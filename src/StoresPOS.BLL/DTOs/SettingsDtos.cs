using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.DTOs;

public class StoreSettingsFormDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? AddressAr { get; set; }
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    public string? CommercialRegister { get; set; }
    public string? ReceiptFooterAr { get; set; }
    public string? ReceiptFooterEn { get; set; }
    public string ReceiptLanguage { get; set; } = "ar";
    public string ReceiptDigits { get; set; } = "western";
    public bool PricesIncludeVat { get; set; } = true;
    public decimal VatPercent { get; set; } = 14m;
    public int ReturnWindowDays { get; set; } = 14;
    public int PaperWidthMm { get; set; } = 80;
    public string DefaultReceiptFormat { get; set; } = "80mm";
    public int SellingWarehouseId { get; set; }
    public int DefaultTreasuryId { get; set; }
    public string InvoiceNumberPrefix { get; set; } = "S-";
    public string ReturnNumberPrefix { get; set; } = "R-";
    public string DefaultUiLanguage { get; set; } = "ar";
    public bool AllowSellAtZeroStock { get; set; }
    public string? LogoRelativePath { get; set; }
    public string? UiLogoRelativePath { get; set; }
    public string? FaviconRelativePath { get; set; }
    public string? DefaultPrinterName { get; set; }
    public string? ThermalPrinterName { get; set; }
    public string? A4PrinterName { get; set; }
    public DocumentPrintSettings SalePrint { get; set; } = new();
    public DocumentPrintSettings ReturnPrint { get; set; } = new();
    public DocumentPrintSettings PurchasePrint { get; set; } = new();
    public DocumentPrintSettings ShiftPrint { get; set; } = new();
    public bool ShowLogoOn80mm { get; set; }
    public bool ShowBarcode { get; set; } = true;
    public decimal ReceiptFontScale { get; set; } = 1m;
    public bool ShowAddressOnReceipt { get; set; } = true;
    public bool ShowPhoneOnReceipt { get; set; } = true;
    public bool ShowTaxNumberOnReceipt { get; set; } = true;
    public bool A4ShowLogo { get; set; } = true;
    public bool A4ShowCommercialRegister { get; set; } = true;
    public string UiTheme { get; set; } = "light";
    public string? UiPrimaryColor { get; set; }
    public string? UiSidebarColor { get; set; }
    public string BackupFolder { get; set; } = @"C:\StoresPOS\Backups";
    public bool BackupOnShiftClose { get; set; } = true;
    public bool DailyBackupEnabled { get; set; } = true;
    public int DailyBackupHourLocal { get; set; } = 2;
}

public class BackupResultDto
{
    public bool Succeeded { get; set; }
    public string? FilePath { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class BackupFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class LicenseStatusDto
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsActivated { get; set; }
    public string? LicensedTo { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public bool IsExpired { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

public class LicenseActivateDto
{
    public string LicensedTo { get; set; } = string.Empty;
    public DateTime? ExpiresUtc { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
