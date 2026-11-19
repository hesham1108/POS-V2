namespace StoresPOS.BLL.Models;

/// <summary>
/// Store profile and receipt options (section "Store" in appsettings).
/// Phase 11 moves these into the Settings screen; until then edit appsettings.json on the store PC.
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
    public string? ReceiptFooterAr { get; set; } = "شكرًا لزيارتكم";
    public string? ReceiptFooterEn { get; set; } = "Thank you for your visit";

    /// <summary>Receipts print in the store's language, not the cashier's UI language ("ar" or "en").</summary>
    public string ReceiptLanguage { get; set; } = "ar";

    /// <summary>"western" (123) or "arabic" (١٢٣) digits on printed receipts. Screens always use Western.</summary>
    public string ReceiptDigits { get; set; } = "western";

    /// <summary>Open decision default: prices include VAT and the receipt shows the included amount.</summary>
    public bool PricesIncludeVat { get; set; } = true;
    public decimal VatPercent { get; set; } = 14m;

    /// <summary>Paper roll width; the printable area is ~8 mm narrower.</summary>
    public int PaperWidthMm { get; set; } = 80;

    public string NameFor(string language) => language == "ar" ? NameAr : NameEn;
    public string? AddressFor(string language) => language == "ar" ? AddressAr : AddressEn;
    public string? FooterFor(string language) => language == "ar" ? ReceiptFooterAr : ReceiptFooterEn;

    /// <summary>VAT contained in a VAT-inclusive total; 0 when prices exclude VAT or the rate is 0.</summary>
    public decimal IncludedVat(decimal total) =>
        PricesIncludeVat && VatPercent > 0
            ? Math.Round(total - total / (1 + VatPercent / 100m), 2, MidpointRounding.AwayFromZero)
            : 0m;
}
