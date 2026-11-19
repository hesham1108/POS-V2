using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.DAL.Context;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Journeys E (cash-out) and G (return) over HTTP, and how both move the drawer.</summary>
[Collection("app")]
public class ReturnsJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ReturnsJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CashOut_ThenReturn_UpdateDrawer_Stock_AndLists()
    {
        await _factory.ResetShiftsAsync();

        // Sale: 2 × oil (78) = 156 cash, opening 100.
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "100" });
        var token = Token(pos.Html);
        Assert.Contains("data-cashout", pos.Html);
        Assert.Contains("id=\"cashout-modal\"", pos.Html);
        Assert.DoesNotContain("/Returns/Create", pos.Html);   // cashier: Returns.Create is OFF by default

        var cart = await Ajax(mona, token, "/Pos/Scan", new() { ["code"] = "622100" });
        var lineId = Regex.Match(cart.Body, "data-line-id=\"(\\d+)\"").Groups[1].Value;
        await Ajax(mona, token, "/Pos/Quantity", new() { ["lineId"] = lineId, ["delta"] = "1" });
        Assert.Equal(HttpStatusCode.OK, (await Ajax(mona, token, "/Pos/Pay", new() { ["method"] = "1", ["amountPaid"] = "200" })).Status);

        // E — cash-out from the POS modal.
        var bad = await Ajax(mona, token, "/CashOuts/Create", new() { ["amount"] = "20", ["reason"] = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Equal("Enter the reason for this cash-out.", bad.Body);

        var cashOut = await Ajax(mona, token, "/CashOuts/Create", new() { ["amount"] = "20", ["reason"] = "Cleaning supplies" });
        Assert.Equal(HttpStatusCode.OK, cashOut.Status);
        using (var json = JsonDocument.Parse(cashOut.Body))
        {
            Assert.Equal(236m, json.RootElement.GetProperty("expectedCash").GetDecimal());   // 100 + 156 − 20
            Assert.False(json.RootElement.GetProperty("warning").GetBoolean());
        }

        var current = await GetAsync(mona, "/Shifts/Current");
        Assert.Contains("Cleaning supplies", current.Html);
        Assert.Matches("data-value=\"236(\\.0+)?\"", current.Html);
        Assert.Contains("Cleaning supplies", (await GetAsync(mona, "/CashOuts")).Html);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Returns/Create")).Url);

        // G — manager/admin scans the receipt number and returns 1 oil in cash.
        var admin = _factory.CreateBrowser();
        await LoginAsync(admin, "admin", "Admin@12345");
        var notFound = await GetAsync(admin, "/Returns/Create?number=S-999999");
        Assert.Contains("No completed invoice with number", notFound.Html);

        var form = await GetAsync(admin, "/Returns/Create?number=S-000001");
        Assert.Contains("saleId=", form.Url);
        Assert.Contains("Return from invoice S-000001", form.Html);
        var itemId = Regex.Match(form.Html, "name=\"Lines\\[0\\].SalesInvoiceItemId\" value=\"(\\d+)\"").Groups[1].Value;
        var invoiceId = Regex.Match(form.Html, "name=\"InvoiceId\" value=\"(\\d+)\"").Groups[1].Value;

        var tooMany = await PostFormAsync(admin, "/Returns/Create", form.Html, new()
        {
            ["InvoiceId"] = invoiceId,
            ["Lines[0].SalesInvoiceItemId"] = itemId,
            ["Lines[0].Quantity"] = "5",
            ["RefundMethod"] = "Cash",
            ["Reason"] = "Damaged"
        });
        Assert.Contains("Only 2 of &quot;Sunflower oil 1 L&quot; can still be returned.", tooMany.Html);

        var saved = await PostFormAsync(admin, "/Returns/Create", form.Html, new()
        {
            ["InvoiceId"] = invoiceId,
            ["Lines[0].SalesInvoiceItemId"] = itemId,
            ["Lines[0].Quantity"] = "1",
            ["RefundMethod"] = "Cash",
            ["Reason"] = "Damaged"
        });
        Assert.Contains("/Returns?printId=", saved.Url);
        Assert.Contains("Return R-000001 saved. Give the customer 78.00 EGP from the drawer.", saved.Html);
        Assert.Contains("data-print-on-load=", saved.Html);

        var returnId = Regex.Match(saved.Url, "printId=(\\d+)").Groups[1].Value;
        var slip = await GetAsync(admin, $"/Receipts/Return/{returnId}");
        Assert.Contains("إيصال مرتجع", slip.Html);
        Assert.Contains("R-000001", slip.Html);
        Assert.Contains("S-000001", slip.Html);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var oil = await db.Products.AsNoTracking().SingleAsync(x => x.Barcode == "622100");
            Assert.Equal(19m, oil.StockQuantity);   // 20 − 2 + 1
        }

        // Drawer: 236 − 78 refund.
        Assert.Matches("data-value=\"158(\\.0+)?\"", (await GetAsync(admin, "/Shifts/Current")).Html);
        Assert.Contains("Partly returned", (await GetAsync(admin, "/Sales")).Html);
        Assert.Contains("R-000001", (await GetAsync(admin, $"/Sales/Details/{invoiceId}")).Html);
        Assert.Contains("R-000001", (await GetAsync(admin, "/Returns")).Html);

        // Cashier may not open the back office slip of a return (no Returns.Create, no Reports.View).
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, $"/Receipts/Return/{returnId}")).Url);
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private sealed record AjaxResult(HttpStatusCode Status, string Body);

    private static async Task<AjaxResult> Ajax(HttpClient client, string token, string url, Dictionary<string, string> data)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(data) };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        return new AjaxResult(response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
