using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 10 over HTTP: POS customer (F4), customers screens, dashboard, reports and shift history.</summary>
[Collection("app")]
public class CustomersAndReportsJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public CustomersAndReportsJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cashier_QuickAddsCustomer_ThenBackOffice_SeesItEverywhere()
    {
        await _factory.ResetShiftsAsync();

        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "50" });
        var token = Token(pos.Html);
        Assert.Contains("id=\"customer-modal\"", pos.Html);
        Assert.DoesNotContain("arrive in Phase 10", pos.Html);

        // F4 — quick add needs a name, then the new customer is on the invoice.
        var noName = await Ajax(mona, token, "/Pos/QuickCustomer", new() { ["name"] = " ", ["phone"] = "0100" });
        Assert.Equal(HttpStatusCode.BadRequest, noName.Status);
        Assert.Equal("Customer name is required.", noName.Body);

        var cart = await Ajax(mona, token, "/Pos/QuickCustomer", new() { ["name"] = "Hany Adel", ["phone"] = "01005551234" });
        Assert.Equal(HttpStatusCode.OK, cart.Status);
        Assert.Contains("Hany Adel", cart.Body);
        Assert.Contains("has-customer", cart.Body);

        var picker = await GetAsync(mona, "/Pos/Customers?term=0100555");
        Assert.Contains("Hany Adel", picker.Html);

        await Ajax(mona, token, "/Pos/Scan", new() { ["code"] = "622100" });
        var pay = await Ajax(mona, token, "/Pos/Pay", new() { ["method"] = "1", ["amountPaid"] = "100" });
        Assert.Equal(HttpStatusCode.OK, pay.Status);
        int invoiceId;
        using (var json = JsonDocument.Parse(pay.Body))
        {
            invoiceId = json.RootElement.GetProperty("invoiceId").GetInt32();
        }

        // Receipt names the customer; the next invoice is walk-in again.
        Assert.Contains("Hany Adel", (await GetAsync(mona, $"/Receipts/Sale/{invoiceId}")).Html);
        Assert.DoesNotContain("has-customer", (await GetAsync(mona, "/Pos/Cart")).Html);

        // Cashier has no back-office access.
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Customers")).Url);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Reports/Sales")).Url);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Shifts/Index")).Url);

        // Back office.
        var admin = _factory.CreateBrowser();
        var home = await LoginAsync(admin, "admin", "Admin@12345");
        Assert.Contains("Net sales", home.Html);
        Assert.Contains("78.00", home.Html);
        var dashDetails = await GetAsync(admin, "/Home/Details");
        Assert.Contains("S-000001", dashDetails.Html);    // activity timeline (loaded after the page)
        Assert.Contains("Running low", dashDetails.Html);
        Assert.Contains("href=\"/Customers\"", home.Html); // sidebar link enabled
        Assert.Contains("href=\"/Reports/Sales\"", home.Html);

        var customers = await GetAsync(admin, "/Customers?search=Hany");
        Assert.Contains("Hany Adel", customers.Html);
        var customerId = Regex.Match(customers.Html, "/Customers/Details/(\\d+)").Groups[1].Value;
        var details = await GetAsync(admin, $"/Customers/Details/{customerId}");
        Assert.Contains("S-000001", details.Html);
        Assert.Contains("78.00", details.Html);

        var sales = await GetAsync(admin, "/Sales");
        Assert.Contains("Hany Adel", sales.Html);
        Assert.Contains("Hany Adel", (await GetAsync(admin, $"/Sales/Details/{invoiceId}")).Html);

        var salesReport = await GetAsync(admin, "/Reports/Sales");
        Assert.Contains("Sales report", salesReport.Html);
        Assert.Contains("78.00", salesReport.Html);
        Assert.Contains("Sunflower oil 1 L", salesReport.Html);   // top products

        foreach (var (url, title) in new[]
                 {
                     ("/Reports/Purchases", "Purchases report"),
                     ("/Reports/Inventory", "Inventory report"),
                     ("/Reports/Shifts", "Shifts report"),
                     ("/Reports/CashOuts", "Cash-outs report")
                 })
        {
            var page = await GetAsync(admin, url);
            Assert.Contains(title, page.Html);
            AssertAssetUrlsResolved(page.Html);
        }

        var inventory = await GetAsync(admin, "/Reports/Inventory");
        Assert.Contains("Sunflower oil 1 L", inventory.Html);

        // Shift history → details.
        var history = await GetAsync(admin, "/Shifts/Index");
        Assert.Contains("Shift history", history.Html);
        var shiftId = Regex.Match(history.Html, "/Shifts/Details/(\\d+)").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(shiftId));
        var shift = await GetAsync(admin, $"/Shifts/Details/{shiftId}");
        Assert.Contains("S-000001", shift.Html);
        Assert.Contains("Hany Adel", shift.Html);
        Assert.Contains("Reprint shift report", shift.Html);
    }

    [Fact]
    public async Task Admin_CreatesCustomer_DuplicatePhoneIsRejected_AndArabicDashboard()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        await LoginAsync(admin, "admin", "Admin@12345");

        var form = await GetAsync(admin, "/Customers/Create");
        var created = await PostFormAsync(admin, "/Customers/Create", form.Html, new()
        {
            ["Name"] = "Corner shop",
            ["Phone"] = "0222",
            ["DefaultSaleType"] = "2",
            ["IsActive"] = "true"
        });
        Assert.Contains("/Customers/Details/", created.Url);
        Assert.Contains("Customer Corner shop added.", created.Html);
        Assert.Contains("Wholesale", created.Html);

        form = await GetAsync(admin, "/Customers/Create");
        var dupe = await PostFormAsync(admin, "/Customers/Create", form.Html, new()
        {
            ["Name"] = "Someone",
            ["Phone"] = "0222",
            ["DefaultSaleType"] = "1",
            ["IsActive"] = "true"
        });
        Assert.Contains("Another customer already uses the phone 0222.", dupe.Html);

        var ar = _factory.CreateBrowser();
        ar.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        var home = await LoginAsync(ar, "admin", "Admin@12345");
        Assert.Contains("dir=\"rtl\"", home.Html);
        Assert.Contains("صافي المبيعات", home.Html);
        Assert.Contains("قاربت تخلص", (await GetAsync(ar, "/Home/Details")).Html);
        Assert.Contains("تقرير المبيعات", (await GetAsync(ar, "/Reports/Sales")).Html);
        Assert.Contains("سجل الورديات", (await GetAsync(ar, "/Shifts/Index")).Html);
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
