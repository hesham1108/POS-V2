using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Controllers;
using StoresPOS.PL.Printing;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 19: printer per paper format, per-document defaults, one settings mapper.</summary>
public class PrintSettingsTests
{
    [Fact]
    public void Printer_IsChosenByFormat_ThenFallbackPrinter_ThenWindowsDefault()
    {
        var s = new StoreSettings
        {
            ThermalPrinterName = "XP-80C",
            A4PrinterName = "HP LaserJet",
            DefaultPrinterName = "Fallback"
        };

        Assert.Equal("XP-80C", SilentPrintService.ResolvePrinterName(s, PrintFormats.Mm80, "Win"));
        Assert.Equal("HP LaserJet", SilentPrintService.ResolvePrinterName(s, PrintFormats.A4, "Win"));

        s.A4PrinterName = null;
        Assert.Equal("Fallback", SilentPrintService.ResolvePrinterName(s, PrintFormats.A4, "Win"));

        s.DefaultPrinterName = " ";
        Assert.Equal("Win", SilentPrintService.ResolvePrinterName(s, PrintFormats.A4, "Win"));
        Assert.Null(SilentPrintService.ResolvePrinterName(s, PrintFormats.A4, null));
    }

    [Fact]
    public void InvoiceOnA4_AndReturnOn80mm_GoToTheirOwnPrinters()
    {
        var s = new StoreSettings
        {
            ThermalPrinterName = "XP-80C",
            A4PrinterName = "HP LaserJet",
            SalePrint = new DocumentPrintSettings { Format = "A4" },
            ReturnPrint = new DocumentPrintSettings { Format = "80mm" }
        };

        var saleFormat = SilentPrintService.ResolveFormat(s, PrintDocumentType.Sale, null);
        var returnFormat = SilentPrintService.ResolveFormat(s, PrintDocumentType.Return, null);

        Assert.Equal(PrintFormats.A4, saleFormat);
        Assert.Equal(PrintFormats.Mm80, returnFormat);
        Assert.Equal("HP LaserJet", SilentPrintService.ResolvePrinterName(s, saleFormat, null));
        Assert.Equal("XP-80C", SilentPrintService.ResolvePrinterName(s, returnFormat, null));
        Assert.True(s.PrefersA4Receipt);
    }

    [Fact]
    public void OlderSettings_WithoutDocumentDefaults_KeepPrintingAsBefore()
    {
        // A store-settings.json written before Phase 19: only the single format + single printer.
        var s = new StoreSettings { DefaultReceiptFormat = "A4", DefaultPrinterName = "Old printer" };

        Assert.Equal(PrintFormats.A4, s.FormatFor(PrintDocumentType.Sale));
        Assert.Equal(PrintFormats.A4, s.FormatFor(PrintDocumentType.Return));
        Assert.Equal(PrintFormats.A4, s.FormatFor(PrintDocumentType.Purchase));
        Assert.Equal(PrintFormats.Mm80, s.FormatFor(PrintDocumentType.Shift));
        Assert.Equal("Old printer", s.PrinterFor(PrintFormats.Mm80));
        Assert.Equal("Old printer", s.PrinterFor(PrintFormats.A4));
        Assert.True(s.PrintSettingsFor(PrintDocumentType.Shift).AutoPrint);
        Assert.Equal(1, s.PrintSettingsFor(PrintDocumentType.Sale).Copies);
    }

    [Fact]
    public void RequestedFormat_WinsOverTheDocumentDefault()
    {
        var s = new StoreSettings { ShiftPrint = new DocumentPrintSettings { Format = "80mm" } };
        Assert.Equal(PrintFormats.A4, SilentPrintService.ResolveFormat(s, PrintDocumentType.Shift, "a4"));
        Assert.Equal(PrintFormats.Mm80, SilentPrintService.ResolveFormat(s, PrintDocumentType.Shift, "bogus"));
    }

    [Fact]
    public void EverySetting_ExistsOnTheDtoAndTheForm()
    {
        var dtoProps = typeof(StoreSettingsFormDto).GetProperties().Select(p => p.Name).ToHashSet();
        var formProps = typeof(StoreSettingsFormViewModel).GetProperties().Select(p => p.Name).ToHashSet();
        var missing = typeof(StoreSettings).GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .Where(n => !dtoProps.Contains(n) || !formProps.Contains(n))
            .ToList();

        Assert.True(missing.Count == 0, "Missing on DTO/form: " + string.Join(", ", missing));
    }

    [Fact]
    public void Mapper_RoundTrip_KeepsEveryValue()
    {
        var original = new StoreSettings
        {
            NameEn = "Al Noor",
            NameAr = "النور",
            VatPercent = 5m,
            ThermalPrinterName = "XP-80C",
            A4PrinterName = "HP",
            ShiftPrint = new DocumentPrintSettings { Format = "A4", AutoPrint = false, Copies = 2 },
            ReceiptFontScale = 1.1m,
            DailyBackupHourLocal = 4
        };

        var back = SettingsMapper.Map<StoreSettings>(SettingsController.ToDto(original));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(back));

        var viaForm = SettingsMapper.Map<StoreSettings>(SettingsController.ToForm(original));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(viaForm));

        // Deep copy: changing the clone never changes the source.
        var clone = SettingsMapper.Clone(original);
        clone.ShiftPrint.Copies = 5;
        Assert.Equal(2, original.ShiftPrint.Copies);
    }

    [Fact]
    public void SavingATab_CopiesOnlyThatTabsFields()
    {
        var dto = SettingsController.ToDto(new StoreSettings
        {
            NameEn = "Keep me",
            LogoRelativePath = "uploads/logo.png",
            VatPercent = 14m
        });
        var form = new StoreSettingsFormViewModel
        {
            NameEn = "Changed",
            VatPercent = 0m,
            A4PrinterName = "HP",
            SalePrint = new DocumentPrintSettings { Format = "A4", AutoPrint = false, Copies = 3 }
        };

        SettingsController.MergeTab("printing", form, dto);

        Assert.Equal("Keep me", dto.NameEn);
        Assert.Equal(14m, dto.VatPercent);
        Assert.Equal("HP", dto.A4PrinterName);
        Assert.Equal("A4", dto.SalePrint.Format);
        Assert.False(dto.SalePrint.AutoPrint);
        Assert.Equal(3, dto.SalePrint.Copies);

        SettingsController.MergeTab("store", form, dto);
        Assert.Equal("Changed", dto.NameEn);
        Assert.Equal("uploads/logo.png", dto.LogoRelativePath); // empty upload path keeps the stored file
    }

    [Fact]
    public void EveryEditableField_BelongsToAKnownTab()
    {
        var orphans = new List<string>();
        foreach (var p in typeof(StoreSettingsFormViewModel).GetProperties())
        {
            var tabs = p.GetCustomAttribute<SettingsTabAttribute>()?.Tabs ?? [];
            if (tabs.Any(t => !SettingsController.Tabs.Contains(t)))
            {
                orphans.Add(p.Name + " (unknown tab)");
            }

            // Fields with a label are shown somewhere; only the legacy format is intentionally off-tab.
            if (tabs.Count == 0 && p.GetCustomAttribute<DisplayAttribute>() is not null && p.Name != nameof(StoreSettingsFormViewModel.DefaultReceiptFormat))
            {
                orphans.Add(p.Name + " (no tab)");
            }
        }

        Assert.True(orphans.Count == 0, string.Join(", ", orphans));
    }
}
