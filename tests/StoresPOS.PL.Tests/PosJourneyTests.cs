using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Journey D over HTTP: scan → quantity → pay, permission gates, and close waiting for a sale.</summary>
[Collection("app")]
public class PosJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public PosJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Scan_Pay_DeductsStock_AndShowsInSalesList()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "100" });
        var token = Token(pos.Html);

        var cart = await Ajax(mona, token, "/Pos/Scan", new() { ["code"] = "622100" });
        Assert.Equal(HttpStatusCode.OK, cart.Status);
        Assert.Contains("Sunflower oil 1 L", cart.Body);
        var lineId = Regex.Match(cart.Body, "data-line-id=\"(\\d+)\"").Groups[1].Value;

        cart = await Ajax(mona, token, "/Pos/Quantity", new() { ["lineId"] = lineId, ["delta"] = "1" });
        Assert.Contains("156.00", cart.Body);

        // Cashier has no price-override permission by default.
        var denied = await Ajax(mona, token, "/Pos/Price", new() { ["lineId"] = lineId, ["price"] = "70" });
        Assert.Equal(HttpStatusCode.BadRequest, denied.Status);
        Assert.Equal("You do not have permission to change prices.", denied.Body);

        var low = await Ajax(mona, token, "/Pos/Pay", new() { ["method"] = "1", ["amountPaid"] = "100" });
        Assert.Equal(HttpStatusCode.BadRequest, low.Status);
        Assert.Equal("Paid 100.00 is less than the total 156.00.", low.Body);

        var paid = await Ajax(mona, token, "/Pos/Pay", new() { ["method"] = "1", ["amountPaid"] = "200" });
        Assert.Equal(HttpStatusCode.OK, paid.Status);
        using var json = JsonDocument.Parse(paid.Body);
        Assert.Equal("S-000001", json.RootElement.GetProperty("invoiceNumber").GetString());
        Assert.Contains("Change: 44.00", json.RootElement.GetProperty("message").GetString());
        var receiptUrl = json.RootElement.GetProperty("receiptUrl").GetString()!;

        // 80 mm receipt: store header, invoice meta, charged prices, tender, change, barcode — in Arabic.
        var receipt = await GetAsync(mona, receiptUrl);
        Assert.Contains("سوبر ماركت النور", receipt.Html);
        Assert.Contains("S-000001", receipt.Html);
        Assert.Contains("زيت عباد الشمس ١ لتر", receipt.Html);
        Assert.Contains("200.00", receipt.Html);
        Assert.Contains("44.00", receipt.Html);
        Assert.Contains("<svg", receipt.Html);
        Assert.Contains("dir=\"rtl\"", receipt.Html);

        // POS now offers "reprint last".
        Assert.Contains($"data-print-url=\"{receiptUrl}\"", (await GetAsync(mona, "/Pos")).Html);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var oil = await db.Products.AsNoTracking().SingleAsync(x => x.Barcode == "622100");
            Assert.Equal(18m, oil.StockQuantity);
        }

        var admin = _factory.CreateBrowser();
        await LoginAsync(admin, "admin", "Admin@12345");
        var list = await GetAsync(admin, "/Sales");
        Assert.Contains("S-000001", list.Html);
        Assert.Contains("156.00", list.Html);

        // Cashier cannot open the sales list (Reports.View).
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Sales")).Url);

        // After the shift closes, the cashier can no longer reprint its receipts; the back office can.
        var close = await GetAsync(admin, "/Shifts/Close");
        var shiftId = Regex.Match(close.Html, "name=\"ShiftId\" value=\"(\\d+)\"").Groups[1].Value;
        var handover = Regex.Match(close.Html, "name=\"HandoverTreasuryId\"[^>]*>\\s*<option[^>]*value=\"(\\d+)\"").Groups[1].Value;
        await PostFormAsync(admin, "/Shifts/Close", close.Html, new()
        {
            ["ShiftId"] = shiftId,
            ["ActualCash"] = "",
            ["HandoverTreasuryId"] = handover,
            ["ReturnTo"] = "",
            ["Notes"] = ""
        });
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, receiptUrl)).Url);
        Assert.Contains("S-000001", (await GetAsync(admin, receiptUrl)).Html);
    }

    [Fact]
    public async Task Close_WaitsForSaleInProgress_UntilHeld()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "" });
        await Ajax(mona, Token(pos.Html), "/Pos/Scan", new() { ["code"] = "622100" });

        var admin = _factory.CreateBrowser();
        await LoginAsync(admin, "admin", "Admin@12345");
        var close = await GetAsync(admin, "/Shifts/Close");
        Assert.Contains("Mona has an invoice in progress", close.Html);
        Assert.Matches("<button type=\"submit\" class=\"btn sp-btn-primary shift-gate__primary\" disabled", close.Html);

        var invoiceId = Regex.Match(close.Html, "name=\"invoiceId\" value=\"(\\d+)\"").Groups[1].Value;
        var afterHold = await PostFormAsync(admin, "/Shifts/HoldInProgress", close.Html, new()
        {
            ["invoiceId"] = invoiceId,
            ["returnTo"] = ""
        });
        Assert.DoesNotContain("has an invoice in progress", afterHold.Html);
        Assert.Contains("1 held invoice(s) will be cancelled", afterHold.Html);

        // POS page for a non-participant goes back to the gate.
        Assert.Contains("/Shifts/Gate", (await GetAsync(admin, "/Pos")).Url);
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
