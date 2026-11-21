using System.Text.Json.Serialization;

namespace StoresPOS.BLL.Models;

/// <summary>
/// Store profile and receipt options (section "Store" in appsettings + App_Data/store-settings.json overlay).
/// </summary>
public class StoreSettings
{
    public const string SectionName = "Store";

    public string NameAr { get; set; } = "متجري";
    public string NameEn { get; set; } = "My Store";
    public string? AddressAr { get; set; }
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    /// <summary>Commercial registration (printed on A4 invoices).</summary>
    public string? CommercialRegister { get; set; }
    public string? ReceiptFooterAr { get; set; } = "شكرًا لزيارتكم";
    public string? ReceiptFooterEn { get; set; } = "Thank you for your visit";

    /// <summary>Receipts print in the store's language, not the cashier's UI language ("ar" or "en").</summary>
    public string ReceiptLanguage { get; set; } = "ar";

    /// <summary>"western" (123) or "arabic" (١٢٣) digits on printed receipts. Screens always use Western.</summary>
    public string ReceiptDigits { get; set; } = "western";

    /// <summary>Open decision default: prices include VAT and the receipt shows the included amount.</summary>
    public bool PricesIncludeVat { get; set; } = true;
    public decimal VatPercent { get; set; } = 14m;

    /// <summary>Open decision default: returns accepted within 14 days of the sale. 0 = no limit.</summary>
    public int ReturnWindowDays { get; set; } = 14;

    /// <summary>Paper roll width; the printable area is ~8 mm narrower.</summary>
    public int PaperWidthMm { get; set; } = 80;

    /// <summary>"80mm" or "A4" — default print after POS pay / reprint last.</summary>
    public string DefaultReceiptFormat { get; set; } = "80mm";

    /// <summary>
    /// Warehouse POS sells from and returns restock into (open decision: one selling warehouse).
    /// 0 = use the warehouse marked IsDefault.
    /// </summary>
    public int SellingWarehouseId { get; set; }

    /// <summary>Default cash treasury for shift handover when the closer does not pick one. 0 = treasury IsDefault.</summary>
    public int DefaultTreasuryId { get; set; }

    /// <summary>Sales invoice number prefix (e.g. S- → S-000001).</summary>
    public string InvoiceNumberPrefix { get; set; } = "S-";

    /// <summary>Return slip number prefix (e.g. R- → R-000001).</summary>
    public string ReturnNumberPrefix { get; set; } = "R-";

    /// <summary>Default UI language for new browsers ("ar" or "en"). Cookie still wins after first visit.</summary>
    public string DefaultUiLanguage { get; set; } = "ar";

    /// <summary>When true, POS may sell even if warehouse qty is 0 (manager override of the open decision).</summary>
    public bool AllowSellAtZeroStock { get; set; }

    /// <summary>Relative path under wwwroot for the A4 logo (e.g. uploads/logo.png).</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>UI/sidebar/login logo; falls back to <see cref="LogoRelativePath"/> when empty.</summary>
    public string? UiLogoRelativePath { get; set; }

    /// <summary>Favicon under wwwroot (e.g. uploads/favicon.ico).</summary>
    public string? FaviconRelativePath { get; set; }

    /// <summary>Windows printer name used as the store default (synced with the OS default when possible).</summary>
    public string? DefaultPrinterName { get; set; }

    /// <summary>Printer for 80/58 mm documents. Empty = <see cref="DefaultPrinterName"/> (Phase 19).</summary>
    public string? ThermalPrinterName { get; set; }

    /// <summary>Printer for A4 documents. Empty = <see cref="DefaultPrinterName"/> (Phase 19).</summary>
    public string? A4PrinterName { get; set; }

    /// <summary>Per-document print defaults (Phase 19). Empty values fall back to the older single settings.</summary>
    public DocumentPrintSettings SalePrint { get; set; } = new();
    public DocumentPrintSettings ReturnPrint { get; set; } = new();
    public DocumentPrintSettings PurchasePrint { get; set; } = new();
    public DocumentPrintSettings ShiftPrint { get; set; } = new();

    /// <summary>Show store logo on 80 mm receipts.</summary>
    public bool ShowLogoOn80mm { get; set; }

    /// <summary>Show Code 128 barcode on 80 mm receipts.</summary>
    public bool ShowBarcode { get; set; } = true;

    /// <summary>Receipt body scale (0.9–1.2).</summary>
    public decimal ReceiptFontScale { get; set; } = 1m;

    public bool ShowAddressOnReceipt { get; set; } = true;
    public bool ShowPhoneOnReceipt { get; set; } = true;
    public bool ShowTaxNumberOnReceipt { get; set; } = true;

    /// <summary>Show logo on A4 invoices.</summary>
    public bool A4ShowLogo { get; set; } = true;

    /// <summary>Show commercial register on A4 invoices.</summary>
    public bool A4ShowCommercialRegister { get; set; } = true;

    /// <summary>"light" or "dark" UI theme.</summary>
    public string UiTheme { get; set; } = "light";

    /// <summary>Primary brand color (#RRGGBB). Empty = theme.css default.</summary>
    public string? UiPrimaryColor { get; set; }

    /// <summary>Sidebar background (#RRGGBB). Empty = theme.css default.</summary>
    public string? UiSidebarColor { get; set; }

    /// <summary>Folder SQL Server writes .bak files into (must be writable by the SQL Server service account).</summary>
    public string BackupFolder { get; set; } = @"C:\StoresPOS\Backups";

    /// <summary>Take a backup after every successful shift close (best-effort; close still succeeds if backup fails).</summary>
    public bool BackupOnShiftClose { get; set; } = true;

    /// <summary>Run one automatic backup per local day at <see cref="DailyBackupHourLocal"/>.</summary>
    public bool DailyBackupEnabled { get; set; } = true;

    /// <summary>Local hour (0–23) for the daily backup.</summary>
    public int DailyBackupHourLocal { get; set; } = 2;

    public string NameFor(string language) => language == "ar" ? NameAr : NameEn;
    public string? AddressFor(string language) => language == "ar" ? AddressAr : AddressEn;
    public string? FooterFor(string language) => language == "ar" ? ReceiptFooterAr : ReceiptFooterEn;

    /// <summary>The sale invoice prints on A4 by default (POS choice, reprint last, silent print).</summary>
    [JsonIgnore]
    public bool PrefersA4Receipt => FormatFor(PrintDocumentType.Sale) == PrintFormats.A4;

    /// <summary>
    /// Paper format for a document: its own setting, otherwise the value an older settings file implies
    /// (sale/return: <see cref="DefaultReceiptFormat"/>, purchase: A4, shift report: 80 mm).
    /// </summary>
    public string FormatFor(PrintDocumentType document)
    {
        var own = PrintSettingsFor(document).Format;
        if (PrintFormats.IsKnown(own))
        {
            return PrintFormats.Normalize(own);
        }

        return document switch
        {
            PrintDocumentType.Purchase => PrintFormats.A4,
            PrintDocumentType.Shift => PrintFormats.Mm80,
            _ => PrintFormats.Normalize(DefaultReceiptFormat)
        };
    }

    public DocumentPrintSettings PrintSettingsFor(PrintDocumentType document) => document switch
    {
        PrintDocumentType.Return => ReturnPrint ??= new(),
        PrintDocumentType.Purchase => PurchasePrint ??= new(),
        PrintDocumentType.Shift => ShiftPrint ??= new(),
        _ => SalePrint ??= new()
    };

    /// <summary>Configured printer for a paper format: format printer → store default printer → null (Windows default).</summary>
    public string? PrinterFor(string? format)
    {
        var specific = PrintFormats.Normalize(format) == PrintFormats.A4 ? A4PrinterName : ThermalPrinterName;
        if (!string.IsNullOrWhiteSpace(specific))
        {
            return specific.Trim();
        }

        return string.IsNullOrWhiteSpace(DefaultPrinterName) ? null : DefaultPrinterName.Trim();
    }

    [JsonIgnore]
    public string EffectiveUiLogoPath =>
        !string.IsNullOrWhiteSpace(UiLogoRelativePath) ? UiLogoRelativePath! : LogoRelativePath ?? string.Empty;

    [JsonIgnore]
    public bool IsDarkTheme =>
        string.Equals(UiTheme, "dark", StringComparison.OrdinalIgnoreCase);

    /// <summary>VAT contained in a VAT-inclusive total; 0 when prices exclude VAT or the rate is 0.</summary>
    public decimal IncludedVat(decimal total) =>
        PricesIncludeVat && VatPercent > 0
            ? Math.Round(total - total / (1 + VatPercent / 100m), 2, MidpointRounding.AwayFromZero)
            : 0m;
}

/// <summary>Documents with their own print defaults.</summary>
public enum PrintDocumentType
{
    Sale,
    Return,
    Purchase,
    Shift
}

public static class PrintFormats
{
    public const string Mm80 = "80mm";
    public const string A4 = "A4";

    public static bool IsKnown(string? value) =>
        string.Equals(value, A4, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Mm80, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        string.Equals(value, A4, StringComparison.OrdinalIgnoreCase) ? A4 : Mm80;
}

/// <summary>Print defaults of one document type (Phase 19).</summary>
public class DocumentPrintSettings
{
    /// <summary>"80mm", "A4", or empty = fall back to the older store-wide setting.</summary>
    public string? Format { get; set; }

    /// <summary>Print automatically after the document is saved (POS pay, return saved, shift closed).</summary>
    public bool AutoPrint { get; set; } = true;

    /// <summary>Copies for silent printing (1–5). Browser printing always prints one.</summary>
    public int Copies { get; set; } = 1;
}

public class LicenseOptions
{
    public const string SectionName = "License";

    /// <summary>HMAC key used to verify (and for us to issue) offline license files.</summary>
    public string IssuerSecret { get; set; } = "CHANGE_ME_LICENSE_HMAC_SECRET_32CHARS_MIN!!";
}
