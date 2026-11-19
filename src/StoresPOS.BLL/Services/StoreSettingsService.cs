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

        var root = new JsonObject
        {
            [StoreSettings.SectionName] = new JsonObject
            {
                ["NameAr"] = form.NameAr.Trim(),
                ["NameEn"] = form.NameEn.Trim(),
                ["AddressAr"] = NullIfEmpty(form.AddressAr),
                ["AddressEn"] = NullIfEmpty(form.AddressEn),
                ["Phone"] = NullIfEmpty(form.Phone),
                ["TaxNumber"] = NullIfEmpty(form.TaxNumber),
                ["CommercialRegister"] = NullIfEmpty(form.CommercialRegister),
                ["ReceiptFooterAr"] = NullIfEmpty(form.ReceiptFooterAr),
                ["ReceiptFooterEn"] = NullIfEmpty(form.ReceiptFooterEn),
                ["ReceiptLanguage"] = NormalizeLang(form.ReceiptLanguage),
                ["ReceiptDigits"] = NormalizeDigits(form.ReceiptDigits),
                ["PricesIncludeVat"] = form.PricesIncludeVat,
                ["VatPercent"] = form.VatPercent,
                ["ReturnWindowDays"] = form.ReturnWindowDays,
                ["PaperWidthMm"] = form.PaperWidthMm,
                ["DefaultReceiptFormat"] = NormalizeReceiptFormat(form.DefaultReceiptFormat),
                ["SellingWarehouseId"] = form.SellingWarehouseId,
                ["DefaultTreasuryId"] = form.DefaultTreasuryId,
                ["InvoiceNumberPrefix"] = NormalizePrefix(form.InvoiceNumberPrefix, "S-"),
                ["ReturnNumberPrefix"] = NormalizePrefix(form.ReturnNumberPrefix, "R-"),
                ["DefaultUiLanguage"] = NormalizeLang(form.DefaultUiLanguage),
                ["AllowSellAtZeroStock"] = form.AllowSellAtZeroStock,
                ["LogoRelativePath"] = NullIfEmpty(form.LogoRelativePath),
                ["BackupFolder"] = string.IsNullOrWhiteSpace(form.BackupFolder)
                    ? @"C:\StoresPOS\Backups"
                    : form.BackupFolder.Trim(),
                ["BackupOnShiftClose"] = form.BackupOnShiftClose,
                ["DailyBackupEnabled"] = form.DailyBackupEnabled,
                ["DailyBackupHourLocal"] = Math.Clamp(form.DailyBackupHourLocal, 0, 23)
            }
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
    }

    private static string NormalizeLang(string? value) =>
        string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

    private static string NormalizeDigits(string? value) =>
        string.Equals(value, "arabic", StringComparison.OrdinalIgnoreCase) ? "arabic" : "western";

    private static string NormalizeReceiptFormat(string? value) =>
        string.Equals(value, "A4", StringComparison.OrdinalIgnoreCase) ? "A4" : "80mm";

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

    private static JsonNode? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StoreSettings Clone(StoreSettings s) => new()
    {
        NameAr = s.NameAr,
        NameEn = s.NameEn,
        AddressAr = s.AddressAr,
        AddressEn = s.AddressEn,
        Phone = s.Phone,
        TaxNumber = s.TaxNumber,
        CommercialRegister = s.CommercialRegister,
        ReceiptFooterAr = s.ReceiptFooterAr,
        ReceiptFooterEn = s.ReceiptFooterEn,
        ReceiptLanguage = s.ReceiptLanguage,
        ReceiptDigits = s.ReceiptDigits,
        PricesIncludeVat = s.PricesIncludeVat,
        VatPercent = s.VatPercent,
        ReturnWindowDays = s.ReturnWindowDays,
        PaperWidthMm = s.PaperWidthMm,
        DefaultReceiptFormat = s.DefaultReceiptFormat,
        SellingWarehouseId = s.SellingWarehouseId,
        DefaultTreasuryId = s.DefaultTreasuryId,
        InvoiceNumberPrefix = s.InvoiceNumberPrefix,
        ReturnNumberPrefix = s.ReturnNumberPrefix,
        DefaultUiLanguage = s.DefaultUiLanguage,
        AllowSellAtZeroStock = s.AllowSellAtZeroStock,
        LogoRelativePath = s.LogoRelativePath,
        BackupFolder = s.BackupFolder,
        BackupOnShiftClose = s.BackupOnShiftClose,
        DailyBackupEnabled = s.DailyBackupEnabled,
        DailyBackupHourLocal = s.DailyBackupHourLocal
    };
}
