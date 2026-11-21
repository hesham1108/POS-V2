using Microsoft.Extensions.Options;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;

namespace StoresPOS.BLL.Tests;

/// <summary>Phase 24: print templates — defaults from the older settings, saving, reset, clean-up.</summary>
public class PrintTemplateTests
{
    private static (PrintTemplateService Service, string Path) Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "storespos-templates-" + Guid.NewGuid().ToString("N") + ".json");
        return (new PrintTemplateService(Options.Create(new StoreSettingsPersistenceOptions { TemplatesFilePath = path })), path);
    }

    [Fact]
    public void Defaults_FollowTheOlderReceiptSettings()
    {
        var (svc, _) = Create();
        var settings = new StoreSettings { ShowLogoOn80mm = true, A4ShowLogo = false, ShowAddressOnReceipt = false, ShowBarcode = true, ReceiptFontScale = 1.1m };

        var r80 = svc.Get(PrintTemplateCatalog.Sale, "80mm", settings);
        Assert.True(r80.ShowLogo);
        Assert.False(r80.ShowAddress);
        Assert.True(r80.ShowBarcode);
        Assert.Equal(1.1m, r80.FontScale);
        Assert.Contains("catalog", r80.HiddenColumns);        // 80 mm keeps its narrow columns
        Assert.False(r80.ShowSignatures);

        var a4 = svc.Get(PrintTemplateCatalog.Sale, "A4", settings);
        Assert.False(a4.ShowLogo);
        Assert.Empty(a4.HiddenColumns);
        Assert.True(a4.ShowSignatures);
        Assert.True(a4.ShowAmountInWords);
        Assert.False(svc.IsCustomized(PrintTemplateCatalog.Sale, "a4"));
    }

    [Fact]
    public async Task Save_StoresPerDocumentAndSize_AndResetGoesBackToTheDefault()
    {
        var (svc, path) = Create();
        try
        {
            var settings = new StoreSettings();
            var t = svc.Get(PrintTemplateCatalog.Return, "80mm", settings);
            t.SectionOrder = ["footer", "header", "info", "items", "totals"];
            t.HiddenSections = ["info"];
            t.ReturnPolicyEn = "  Returns within 14 days  ";
            t.HiddenColumns = ["price"];
            await svc.SaveAsync(t);

            // A new service instance reads the file back.
            var again = new PrintTemplateService(Options.Create(new StoreSettingsPersistenceOptions { TemplatesFilePath = path }));
            var saved = again.Get(PrintTemplateCatalog.Return, "80mm", settings);
            Assert.Equal("footer", saved.SectionOrder[0]);
            Assert.False(saved.Shows("info"));
            Assert.Equal("Returns within 14 days", saved.ReturnPolicyEn);
            Assert.False(saved.ShowsColumn("price"));
            Assert.True(again.IsCustomized(PrintTemplateCatalog.Return, "80mm"));
            Assert.False(again.IsCustomized(PrintTemplateCatalog.Return, "a4"));   // the other size is separate

            // Changing a returned copy does not change what is stored.
            saved.ShowLogo = !saved.ShowLogo;
            Assert.NotEqual(saved.ShowLogo, again.Get(PrintTemplateCatalog.Return, "80mm", settings).ShowLogo);

            await again.ResetAsync(PrintTemplateCatalog.Return, "80mm");
            Assert.False(again.IsCustomized(PrintTemplateCatalog.Return, "80mm"));
            Assert.Equal(PrintTemplateCatalog.Sections, again.Get(PrintTemplateCatalog.Return, "80mm", settings).SectionOrder);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Normalize_KeepsEverySectionOnce_KnownKeysOnly_AndClampsRanges()
    {
        var t = PrintTemplateCatalog.Normalize(new PrintTemplate
        {
            Document = PrintTemplateCatalog.Sale,
            Format = PrintTemplateCatalog.Mm80,
            SectionOrder = ["totals", "totals", "bogus", "header"],
            HiddenColumns = ["price", "nope"],
            HiddenInfo = ["opened"],              // shift key, not a sale key
            FontScale = 5m,
            MarginMm = 0,
            ItemNames = "klingon",
            Density = "tiny"
        });

        Assert.Equal(["totals", "header", "info", "items", "footer"], t.SectionOrder);
        Assert.Equal(["price"], t.HiddenColumns);
        Assert.Empty(t.HiddenInfo);
        Assert.Equal(1.4m, t.FontScale);
        Assert.Equal(3, t.MarginMm);
        Assert.Equal("store", t.ItemNames);
        Assert.Equal("normal", t.Density);
    }

    [Fact]
    public async Task Save_RejectsADocumentWithoutThatSize()
    {
        var (svc, _) = Create();
        var t = new PrintTemplate { Document = PrintTemplateCatalog.Purchase, Format = PrintTemplateCatalog.Mm80 };
        await Assert.ThrowsAsync<BusinessException>(() => svc.SaveAsync(t));
    }
}
