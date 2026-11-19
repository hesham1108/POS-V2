using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;

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
}

public class StoreSettingsFormViewModel
{
    [Required, MaxLength(120), Display(Name = "Arabic name")]
    public string NameAr { get; set; } = string.Empty;

    [Required, MaxLength(120), Display(Name = "English name")]
    public string NameEn { get; set; } = string.Empty;

    [MaxLength(300), Display(Name = "Arabic address")]
    public string? AddressAr { get; set; }

    [MaxLength(300), Display(Name = "English address")]
    public string? AddressEn { get; set; }

    [MaxLength(40), Display(Name = "Phone")]
    public string? Phone { get; set; }

    [MaxLength(60), Display(Name = "Tax number")]
    public string? TaxNumber { get; set; }

    [MaxLength(60), Display(Name = "Commercial register")]
    public string? CommercialRegister { get; set; }

    [MaxLength(300), Display(Name = "Arabic receipt footer")]
    public string? ReceiptFooterAr { get; set; }

    [MaxLength(300), Display(Name = "English receipt footer")]
    public string? ReceiptFooterEn { get; set; }

    [Display(Name = "Receipt language")]
    public string ReceiptLanguage { get; set; } = "ar";

    [Display(Name = "Receipt digits")]
    public string ReceiptDigits { get; set; } = "western";

    [Display(Name = "Prices include VAT")]
    public bool PricesIncludeVat { get; set; } = true;

    [Range(0, 100), Display(Name = "VAT %")]
    public decimal VatPercent { get; set; } = 14m;

    [Range(0, 3650), Display(Name = "Return window (days)")]
    public int ReturnWindowDays { get; set; } = 14;

    [Display(Name = "Allow sell at zero stock")]
    public bool AllowSellAtZeroStock { get; set; }

    [Display(Name = "Paper width (mm)")]
    public int PaperWidthMm { get; set; } = 80;

    [Display(Name = "Default receipt format")]
    public string DefaultReceiptFormat { get; set; } = "80mm";

    [Display(Name = "Selling warehouse")]
    public int SellingWarehouseId { get; set; }

    [Display(Name = "Default treasury")]
    public int DefaultTreasuryId { get; set; }

    [MaxLength(10), Display(Name = "Sales invoice prefix")]
    public string InvoiceNumberPrefix { get; set; } = "S-";

    [MaxLength(10), Display(Name = "Return prefix")]
    public string ReturnNumberPrefix { get; set; } = "R-";

    [Display(Name = "Default UI language")]
    public string DefaultUiLanguage { get; set; } = "ar";

    public string? LogoRelativePath { get; set; }

    [Required, MaxLength(260), Display(Name = "Backup folder")]
    public string BackupFolder { get; set; } = @"C:\StoresPOS\Backups";

    [Display(Name = "Backup on shift close")]
    public bool BackupOnShiftClose { get; set; } = true;

    [Display(Name = "Daily automatic backup")]
    public bool DailyBackupEnabled { get; set; } = true;

    [Range(0, 23), Display(Name = "Daily backup hour (local)")]
    public int DailyBackupHourLocal { get; set; } = 2;
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
