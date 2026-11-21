using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Printing;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 24: one document model drives the page and silent printing; the designer saves per document and size.</summary>
public class PrintTemplateJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public PrintTemplateJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    private PrintDocumentBuilder Builder() =>
        new(_factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>());

    private static StoreSettings Settings() => new() { NameEn = "Al Noor", NameAr = "النور", ReceiptLanguage = "en", Phone = "0100", ReceiptFooterEn = "Thanks!" };

    [Fact]
    public void Template_HidesColumnsInfoAndTotals_InTheDocument()
    {
        var s = Settings();
        var t = PrintTemplateCatalog.Default(PrintTemplateCatalog.Sale, PrintTemplateCatalog.A4, s);
        t.HiddenColumns = ["catalog", "discount"];
        t.HiddenInfo = ["cashier"];
        t.HiddenTotals = ["vat", "count"];
        t.ItemNames = "both";

        var doc = Builder().Sale(ReceiptSampleFactory.CreateSampleInvoice(s), new ReceiptFormat(s), t, "Cash", true, null);

        Assert.DoesNotContain(doc.Columns, c => c.Key is "catalog" or "discount");
        Assert.All(doc.Rows, r => Assert.Equal(doc.Columns.Count, r.Length));
        Assert.DoesNotContain(doc.Info, r => r.Key == "cashier");
        Assert.DoesNotContain(doc.Totals, r => r.Key is "vat" or "count");
        Assert.Contains(doc.Rows, r => r.Contains("منتج تجريبي ١ / Sample product 1"));
        Assert.NotNull(doc.AmountInWords);
    }

    [Fact]
    public void SilentPrintLines_FollowTheSameTemplate()
    {
        var s = Settings();
        var t = PrintTemplateCatalog.Default(PrintTemplateCatalog.Sale, PrintTemplateCatalog.Mm80, s);
        t.SectionOrder = ["footer", "items", "totals", "header", "info"];
        t.HiddenSections = ["info"];
        t.ShowPhone = false;
        t.HeaderLineEn = "Open 24 hours";
        t.ReturnPolicyEn = "No returns after 14 days";

        var doc = Builder().Sale(ReceiptSampleFactory.CreateSampleInvoice(s), new ReceiptFormat(s), t, "Cash", true, null);
        var lines = SilentPrintService.BuildLines(doc).Select(l => l.Text).ToList();

        var footer = lines.IndexOf("Thanks!");
        var policy = lines.IndexOf("No returns after 14 days");
        var item = lines.FindIndex(l => l.Contains("Sample product 1"));
        var store = lines.IndexOf("Al Noor");
        Assert.True(footer >= 0 && policy > footer && item > policy && store > item, string.Join(" | ", lines));
        Assert.Contains("Open 24 hours", lines);
        Assert.DoesNotContain(lines, l => l.Contains("0100"));                 // phone switched off
        Assert.DoesNotContain(lines, l => l.StartsWith("Cashier", StringComparison.Ordinal)); // info section hidden
        Assert.Contains(lines, l => l.Contains(doc.Title) && l.Contains(doc.Number));        // title moves to the header
    }

    [Fact]
    public async Task Designer_SavesATemplate_ThatPrintoutsUse_AndResetUndoesIt()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");

        foreach (var (doc, format) in PrintTemplateCatalog.All)
        {
            var page = await GetAsync(admin, $"/Settings?tab=templates&doc={doc}&format={format}");
            Assert.Contains("id=\"template-editor\"", page.Html);
            Assert.Contains("/js/templates.js", page.Html);
        }

        var designer = await GetAsync(admin, "/Settings?tab=templates&doc=sale&format=80mm");
        var json = WebUtility(Regex.Match(designer.Html, "data-template=\"([^\"]+)\"").Groups[1].Value);
        var t = JsonSerializer.Deserialize<PrintTemplate>(json)!;
        t.HeaderLineEn = "Welcome to Al Noor";
        t.HeaderLineAr = "Welcome to Al Noor"; // receipts print in the store language (Arabic by default)
        t.SectionOrder = ["header", "items", "info", "totals", "footer"];
        t.HiddenColumns = [.. t.HiddenColumns, "price"];
        var payload = JsonSerializer.Serialize(t);

        // Preview shows the unsaved template on a sample.
        var preview = await PostFormAsync(admin, "/Receipts/TemplatePreview", designer.Html, new() { ["template"] = payload });
        Assert.Contains("Welcome to Al Noor", preview.Html);

        var saved = await PostFormAsync(admin, "/Settings/SaveTemplate", designer.Html, new() { ["template"] = payload });
        Assert.Contains("tab=templates", saved.Url);
        Assert.Contains("Customized", saved.Html);

        var receipt = await GetAsync(admin, "/Receipts/Sample80");
        Assert.Contains("Welcome to Al Noor", receipt.Html);
        Assert.True(receipt.Html.IndexOf("data-section=\"items\"", StringComparison.Ordinal) < receipt.Html.IndexOf("data-section=\"info\"", StringComparison.Ordinal));
        var head = Regex.Match(receipt.Html, "<table class=\"receipt__lines\" data-section=\"items\">\\s*<thead>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(3, Regex.Matches(head, "<th").Count);                 // item, qty, total — price hidden
        Assert.DoesNotContain("Welcome to Al Noor", (await GetAsync(admin, "/Receipts/SampleA4")).Html); // A4 is separate

        await PostFormAsync(admin, "/Settings/ResetTemplate", saved.Html, new() { ["doc"] = "sale", ["format"] = "80mm" });
        Assert.DoesNotContain("Welcome to Al Noor", (await GetAsync(admin, "/Receipts/Sample80")).Html);
    }

    private static string WebUtility(string html) => System.Net.WebUtility.HtmlDecode(html);
}
