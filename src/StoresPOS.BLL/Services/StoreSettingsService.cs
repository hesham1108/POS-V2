using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.Services;

public class StoreSettingsService : IStoreSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IOptionsMonitor<StoreSettings> _monitor;
    private readonly StoreSettingsPersistenceOptions _paths;

    public StoreSettingsService(
        IOptionsMonitor<StoreSettings> monitor,
        IOptions<StoreSettingsPersistenceOptions> paths)
    {
        _monitor = monitor;
        _paths = paths.Value;
    }

    public StoreSettings Current => _monitor.CurrentValue;

    public Task<StoreSettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Clone(Current));

    public async Task SaveAsync(StoreSettingsFormDto form, CancellationToken cancellationToken = default)
    {
        Validate(form);

        if (string.IsNullOrWhiteSpace(_paths.FilePath))
        {
            throw new BusinessException("Store settings file path is not configured.");
        }

        var dir = Path.GetDirectoryName(_paths.FilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var settings = Normalize(form);
        var root = new JsonObject
        {
            [StoreSettings.SectionName] = JsonSerializer.SerializeToNode(settings, JsonOptions)
        };

        var json = root.ToJsonString(JsonOptions);
        await File.WriteAllTextAsync(_paths.FilePath, json, cancellationToken);

        // Give the configuration file watcher a moment to reload (reloadOnChange).
        await Task.Delay(150, cancellationToken);
    }

    private static void Validate(StoreSettingsFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.NameEn) || string.IsNullOrWhiteSpace(form.NameAr))
        {
            throw new BusinessException("English and Arabic store names are required.");
        }

        if (form.VatPercent is < 0 or > 100)
        {
            throw new BusinessException("VAT percent must be between 0 and 100.");
        }

        if (form.ReturnWindowDays < 0)
        {
            throw new BusinessException("Return window days cannot be negative.");
        }

        if (form.PaperWidthMm is not (58 or 80))
        {
            throw new BusinessException("Paper width must be 58 or 80 mm.");
        }

        if (form.ReceiptFontScale is < 0.9m or > 1.2m)
        {
            throw new BusinessException("Receipt font scale must be between 0.9 and 1.2.");
        }

        if (!string.IsNullOrWhiteSpace(form.UiPrimaryColor) && NormalizeHex(form.UiPrimaryColor) is null)
        {
            throw new BusinessException("Primary color must be a hex value like #0f6751.");
        }

        if (!string.IsNullOrWhiteSpace(form.UiSidebarColor) && NormalizeHex(form.UiSidebarColor) is null)
        {
            throw new BusinessException("Sidebar color must be a hex value like #1a1d26.");
        }
    }

    private static string NormalizeLang(string? value) =>
        string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

    private static string NormalizeDigits(string? value) =>
        string.Equals(value, "arabic", StringComparison.OrdinalIgnoreCase) ? "arabic" : "western";

    private static string NormalizeReceiptFormat(string? value) =>
        string.Equals(value, "A4", StringComparison.OrdinalIgnoreCase) ? "A4" : "80mm";

    private static string NormalizeTheme(string? value) =>
        string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var v = value.Trim();
        if (v.Length == 6 && v[0] != '#')
        {
            v = "#" + v;
        }

        if (v.Length != 7 || v[0] != '#')
        {
            return null;
        }

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(v[i]))
            {
                return null;
            }
        }

        return v.ToLowerInvariant();
    }

    private static string NormalizePrefix(string? value, string fallback)
    {
        var p = (value ?? string.Empty).Trim();
        if (p.Length == 0)
        {
            return fallback;
        }

        if (p.Length > 10)
        {
            throw new BusinessException("Number prefix is too long (10 characters max).");
        }

        return p.EndsWith('-') ? p : p + "-";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StoreSettings Clone(StoreSettings s) => SettingsMapper.Clone(s);

    /// <summary>The form as it is stored: every field copied (<see cref="SettingsMapper"/>), then cleaned up.</summary>
    internal static StoreSettings Normalize(StoreSettingsFormDto form)
    {
        var s = SettingsMapper.Map<StoreSettings>(form);
        s.NameAr = form.NameAr.Trim();
        s.NameEn = form.NameEn.Trim();
        s.AddressAr = NullIfEmpty(form.AddressAr);
        s.AddressEn = NullIfEmpty(form.AddressEn);
        s.Phone = NullIfEmpty(form.Phone);
        s.TaxNumber = NullIfEmpty(form.TaxNumber);
        s.CommercialRegister = NullIfEmpty(form.CommercialRegister);
        s.ReceiptFooterAr = NullIfEmpty(form.ReceiptFooterAr);
        s.ReceiptFooterEn = NullIfEmpty(form.ReceiptFooterEn);
        s.ReceiptLanguage = NormalizeLang(form.ReceiptLanguage);
        s.ReceiptDigits = NormalizeDigits(form.ReceiptDigits);
        s.DefaultReceiptFormat = NormalizeReceiptFormat(form.DefaultReceiptFormat);
        s.InvoiceNumberPrefix = NormalizePrefix(form.InvoiceNumberPrefix, "S-");
        s.ReturnNumberPrefix = NormalizePrefix(form.ReturnNumberPrefix, "R-");
        s.DefaultUiLanguage = NormalizeLang(form.DefaultUiLanguage);
        s.LogoRelativePath = NullIfEmpty(form.LogoRelativePath);
        s.UiLogoRelativePath = NullIfEmpty(form.UiLogoRelativePath);
        s.FaviconRelativePath = NullIfEmpty(form.FaviconRelativePath);
        s.DefaultPrinterName = NullIfEmpty(form.DefaultPrinterName);
        s.ThermalPrinterName = NullIfEmpty(form.ThermalPrinterName);
        s.A4PrinterName = NullIfEmpty(form.A4PrinterName);
        s.SalePrint = NormalizeDocument(form.SalePrint);
        s.ReturnPrint = NormalizeDocument(form.ReturnPrint);
        s.PurchasePrint = NormalizeDocument(form.PurchasePrint);
        s.ShiftPrint = NormalizeDocument(form.ShiftPrint);
        s.ReceiptFontScale = Math.Clamp(form.ReceiptFontScale, 0.9m, 1.2m);
        s.UiTheme = NormalizeTheme(form.UiTheme);
        s.UiPrimaryColor = NormalizeHex(form.UiPrimaryColor);
        s.UiSidebarColor = NormalizeHex(form.UiSidebarColor);
        s.BackupFolder = string.IsNullOrWhiteSpace(form.BackupFolder) ? @"C:\StoresPOS\Backups" : form.BackupFolder.Trim();
        s.DailyBackupHourLocal = Math.Clamp(form.DailyBackupHourLocal, 0, 23);
        return s;
    }

    private static DocumentPrintSettings NormalizeDocument(DocumentPrintSettings? d) => new()
    {
        Format = d is not null && PrintFormats.IsKnown(d.Format) ? PrintFormats.Normalize(d.Format) : null,
        AutoPrint = d?.AutoPrint ?? true,
        Copies = Math.Clamp(d?.Copies ?? 1, 1, 5)
    };
}
