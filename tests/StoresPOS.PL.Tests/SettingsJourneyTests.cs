using System.Text.Json;
using StoresPOS.PL.Controllers;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 18: settings page split into tabs that load only their own data.</summary>
public class SettingsJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public SettingsJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryTab_Opens_WithGroupedNavigation_AndSearchIndex()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");

        foreach (var tab in SettingsController.Tabs)
        {
            var page = await GetAsync(admin, "/Settings?tab=" + tab);
            Assert.Contains("class=\"sp-settings\" data-tab=\"" + tab + "\"", page.Html);
            Assert.Contains("id=\"settings-search\"", page.Html);
            Assert.Contains("Printers and defaults", page.Html);
            Assert.Contains("/js/settings.js", page.Html);
            AssertAssetUrlsResolved(page.Html);
        }

        var unknown = await GetAsync(admin, "/Settings?tab=nope");
        Assert.Contains("data-tab=\"store\"", unknown.Html);
    }

    [Fact]
    public async Task PrintingTab_LoadsPrintersAfterThePage_AndShowsDocumentDefaults()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");

        var page = await GetAsync(admin, "/Settings?tab=printing");
        Assert.Contains("data-printers-url=\"/Settings/Printers\"", page.Html);
        Assert.Contains("Document defaults", page.Html);
        Assert.Contains("name=\"Form.ShiftPrint.Format\"", page.Html);
        Assert.Contains("name=\"Form.ThermalPrinterName\"", page.Html);
        Assert.Contains("name=\"Form.A4PrinterName\"", page.Html);
        // One preview frame, loaded by script only when visible.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(page.Html, "<iframe"));
        Assert.DoesNotContain("<iframe id=\"receipt-preview\" title=\"Preview\" class=\"sp-receipt-preview\" src=", page.Html);

        var json = await admin.GetStringAsync("/Settings/Printers");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("printers", out var printers));
        Assert.Equal(JsonValueKind.Array, printers.ValueKind);
        Assert.Contains("Test Thermal 80", json);
    }

    [Fact]
    public async Task SettingsPage_InArabic_IsTranslated()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        await LoginAsync(admin, "admin", "Admin@12345");

        var page = await GetAsync(admin, "/Settings?tab=printing");
        Assert.Contains("dir=\"rtl\"", page.Html);
        Assert.DoesNotContain(">Document defaults<", page.Html);
        Assert.DoesNotContain(">Unsaved changes<", page.Html);
        Assert.DoesNotContain(">Printers and defaults<", page.Html);
    }
}
