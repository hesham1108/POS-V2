using System.Text.Json;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 22: period dashboard — figures with the page, charts (JSON) and details (partial) after.</summary>
public class DashboardJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public DashboardJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("yesterday")]
    [InlineData("7")]
    [InlineData("30")]
    [InlineData("month")]
    public async Task EveryPeriod_RendersFiguresChartsAndDetails(string? period)
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");

        var q = period is null ? "" : "?period=" + period;
        var page = await GetAsync(admin, "/" + q);
        Assert.Contains("class=\"dash\"", page.Html);
        Assert.Contains("Net sales", page.Html);
        Assert.Contains("Average invoice", page.Html);
        Assert.Contains("/lib/chartjs/dist/chart.umd.js", page.Html);
        Assert.Contains("aria-current=\"page\"", page.Html);
        AssertAssetUrlsResolved(page.Html);

        var json = await admin.GetStringAsync("/Home/Charts" + q);
        using var doc = JsonDocument.Parse(json);
        var labels = doc.RootElement.GetProperty("labels").GetArrayLength();
        Assert.True(labels > 0);
        Assert.Equal(labels, doc.RootElement.GetProperty("current").GetArrayLength());
        Assert.Equal(labels, doc.RootElement.GetProperty("previous").GetArrayLength());
        Assert.Equal(period is null or "yesterday", doc.RootElement.GetProperty("hourly").GetBoolean());

        var details = await GetAsync(admin, "/Home/Details" + q);
        Assert.Contains("Top products", details.Html);
        Assert.Contains("Latest activity", details.Html);
        Assert.DoesNotContain("<html", details.Html);   // partial, not a full page
    }

    [Fact]
    public async Task Cashier_GoesToTheShiftGate_AndCannotReadDashboardData()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        await LoginAsync(mona, "mona", CashierPassword);
        Assert.Contains("/Shifts/Gate", (await GetAsync(mona, "/")).Url);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Home/Charts")).Url);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Home/Details")).Url);
    }

    [Fact]
    public async Task Dashboard_InArabic_IsTranslatedAndRtl()
    {
        await _factory.ResetShiftsAsync();
        var ar = _factory.CreateBrowser();
        ar.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        await LoginAsync(ar, "admin", "Admin@12345");
        var page = await GetAsync(ar, "/?period=7");
        Assert.Contains("dir=\"rtl\"", page.Html);
        Assert.Contains("صافي المبيعات", page.Html);
        Assert.DoesNotContain(">Net sales<", page.Html);
        var details = await GetAsync(ar, "/Home/Details?period=7");
        Assert.DoesNotContain(">Top products<", details.Html);
    }
}
