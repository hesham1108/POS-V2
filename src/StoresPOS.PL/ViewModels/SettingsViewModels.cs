using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Printing;

namespace StoresPOS.PL.ViewModels;

public class SettingsPageViewModel
{
    public string Tab { get; set; } = "store";
    public StoreSettingsFormViewModel Form { get; set; } = new();
    public IReadOnlyList<WarehouseListItemDto> Warehouses { get; set; } = [];
    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];
    public IReadOnlyList<BackupFileDto> Backups { get; set; } = [];
    public LicenseStatusDto License { get; set; } = new();
    public LicenseActivateViewModel LicenseForm { get; set; } = new();
    public PrinterListDto Printers { get; set; } = new();

    /// <summary>Print template designer (Phase 24).</summary>
    public PrintTemplate? Template { get; set; }
    public bool TemplateCustomized { get; set; }
}

public class StoreSettingsFormViewModel
{
    [SettingsTab("store", "appearance")]
    [Required, MaxLength(120), Display(Name = "Arabic name")]
    public string NameAr { get; set; } = string.Empty;

    [SettingsTab("store", "appearance")]
    [Required, MaxLength(120), Display(Name = "English name")]
    public string NameEn { get; set; } = string.Empty;

    [SettingsTab("store")]
    [MaxLength(300), Display(Name = "Arabic address")]
    public string? AddressAr { get; set; }

    [SettingsTab("store")]
    [MaxLength(300), Display(Name = "English address")]
    public string? AddressEn { get; set; }

    [SettingsTab("store")]
    [MaxLength(40), Display(Name = "Phone")]
    public string? Phone { get; set; }

    [SettingsTab("store")]
    [MaxLength(60), Display(Name = "Tax number")]
    public string? TaxNumber { get; set; }

    [SettingsTab("store")]
    [MaxLength(60), Display(Name = "Commercial register")]
    public string? CommercialRegister { get; set; }

    [SettingsTab("printing")]
    [MaxLength(300), Display(Name = "Arabic receipt footer")]
    public string? ReceiptFooterAr { get; set; }

    [SettingsTab("printing")]
    [MaxLength(300), Display(Name = "English receipt footer")]
    public string? ReceiptFooterEn { get; set; }

    [SettingsTab("printing")]
    [Display(Name = "Receipt language")]
    public string ReceiptLanguage { get; set; } = "ar";

    [SettingsTab("printing")]
    [Display(Name = "Receipt digits")]
    public string ReceiptDigits { get; set; } = "western";

    [SettingsTab("sales")]
    [Display(Name = "Prices include VAT")]
    public bool PricesIncludeVat { get; set; } = true;

    [SettingsTab("sales")]
    [Range(0, 100), Display(Name = "VAT %")]
    public decimal VatPercent { get; set; } = 14m;

    [SettingsTab("sales")]
    [Range(0, 3650), Display(Name = "Return window (days)")]
    public int ReturnWindowDays { get; set; } = 14;

    [SettingsTab("sales")]
    [Display(Name = "Allow sell at zero stock")]
    public bool AllowSellAtZeroStock { get; set; }

    [SettingsTab("printing")]
    [Display(Name = "Paper width (mm)")]
    public int PaperWidthMm { get; set; } = 80;

    // Older store-wide format, kept as the fallback for files saved before Phase 19.
    // Not on any tab any more: per-document formats (SalePrint, …) replace it.
    [Display(Name = "Default receipt format")]
    public string DefaultReceiptFormat { get; set; } = "80mm";

    [SettingsTab("store")]
    [Display(Name = "Selling warehouse")]
    public int SellingWarehouseId { get; set; }

    [SettingsTab("store")]
    [Display(Name = "Default treasury")]
    public int DefaultTreasuryId { get; set; }

    [SettingsTab("numbering")]
    [MaxLength(10), Display(Name = "Sales invoice prefix")]
    public string InvoiceNumberPrefix { get; set; } = "S-";

    [SettingsTab("numbering")]
    [MaxLength(10), Display(Name = "Return prefix")]
    public string ReturnNumberPrefix { get; set; } = "R-";

    [SettingsTab("language")]
    [Display(Name = "Default UI language")]
    public string DefaultUiLanguage { get; set; } = "ar";

    [SettingsTab("store", "appearance")]
    public string? LogoRelativePath { get; set; }

    [SettingsTab("appearance")]
    public string? UiLogoRelativePath { get; set; }

    [SettingsTab("appearance")]
    public string? FaviconRelativePath { get; set; }

    [SettingsTab("printing")]
    [MaxLength(200), Display(Name = "Fallback printer")]
    public string? DefaultPrinterName { get; set; }

    [SettingsTab("printing"), MaxLength(200), Display(Name = "Thermal printer (80 mm)")]
    public string? ThermalPrinterName { get; set; }

    [SettingsTab("printing"), MaxLength(200), Display(Name = "A4 printer")]
    public string? A4PrinterName { get; set; }

    [SettingsTab("printing")]
    public DocumentPrintSettings SalePrint { get; set; } = new();

    [SettingsTab("printing")]
    public DocumentPrintSettings ReturnPrint { get; set; } = new();

    [SettingsTab("printing")]
    public DocumentPrintSettings PurchasePrint { get; set; } = new();

    [SettingsTab("printing")]
    public DocumentPrintSettings ShiftPrint { get; set; } = new();

    [SettingsTab("printing")]
    [Display(Name = "Show logo on 80 mm")]
    public bool ShowLogoOn80mm { get; set; }

    [SettingsTab("printing")]
    [Display(Name = "Show barcode")]
    public bool ShowBarcode { get; set; } = true;

    [SettingsTab("printing")]
    [Range(0.9, 1.2), Display(Name = "Receipt font scale")]
    public decimal ReceiptFontScale { get; set; } = 1m;

    [SettingsTab("printing")]
    [Display(Name = "Show address on receipt")]
    public bool ShowAddressOnReceipt { get; set; } = true;

    [SettingsTab("printing")]
    [Display(Name = "Show phone on receipt")]
    public bool ShowPhoneOnReceipt { get; set; } = true;

    [SettingsTab("printing")]
    [Display(Name = "Show tax number on receipt")]
    public bool ShowTaxNumberOnReceipt { get; set; } = true;

    [SettingsTab("printing")]
    [Display(Name = "Show logo on A4")]
    public bool A4ShowLogo { get; set; } = true;

    [SettingsTab("printing")]
    [Display(Name = "Show commercial register on A4")]
    public bool A4ShowCommercialRegister { get; set; } = true;

    [SettingsTab("appearance")]
    [Display(Name = "UI theme")]
    public string UiTheme { get; set; } = "light";

    [SettingsTab("appearance")]
    [MaxLength(7), Display(Name = "Primary color")]
    public string? UiPrimaryColor { get; set; }

    [SettingsTab("appearance")]
    [MaxLength(7), Display(Name = "Sidebar color")]
    public string? UiSidebarColor { get; set; }

    [SettingsTab("backup")]
    [Required, MaxLength(260), Display(Name = "Backup folder")]
    public string BackupFolder { get; set; } = @"C:\StoresPOS\Backups";

    [SettingsTab("backup")]
    [Display(Name = "Backup on shift close")]
    public bool BackupOnShiftClose { get; set; } = true;

    [SettingsTab("backup")]
    [Display(Name = "Daily automatic backup")]
    public bool DailyBackupEnabled { get; set; } = true;

    [SettingsTab("backup")]
    [Range(0, 23), Display(Name = "Daily backup hour (local)")]
    public int DailyBackupHourLocal { get; set; } = 2;
}

/// <summary>
/// The settings tab(s) a form field belongs to. Saving a tab copies exactly the fields marked with
/// that tab, so a new field only needs this attribute (Phase 19 — replaces the hand-written switch).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsTabAttribute : Attribute
{
    public SettingsTabAttribute(params string[] tabs)
    {
        Tabs = tabs;
    }

    public IReadOnlyList<string> Tabs { get; }
}

public class LicenseActivateViewModel
{
    [Required, MaxLength(200), Display(Name = "Licensed to")]
    public string LicensedTo { get; set; } = string.Empty;

    [Display(Name = "Expires (optional)")]
    [DataType(DataType.Date)]
    public DateTime? ExpiresUtc { get; set; }

    [Required, MaxLength(128), Display(Name = "Signature")]
    public string Signature { get; set; } = string.Empty;
}
