namespace StoresPOS.BLL.Models;

/// <summary>
/// Layout of one printed document on one paper size (Phase 24, plan §2 — "sections that can be switched
/// on/off and reordered"; a free template language is left for later). Stored as JSON in App_Data.
/// </summary>
public class PrintTemplate
{
    /// <summary>sale | return | purchase | shift</summary>
    public string Document { get; set; } = PrintTemplateCatalog.Sale;

    /// <summary>80mm | a4</summary>
    public string Format { get; set; } = PrintTemplateCatalog.Mm80;

    /// <summary>Order of the five sections (header, info, items, totals, footer).</summary>
    public List<string> SectionOrder { get; set; } = [.. PrintTemplateCatalog.Sections];
    public List<string> HiddenSections { get; set; } = [];

    // ---- header
    public bool ShowLogo { get; set; }
    public bool ShowStoreName { get; set; } = true;
    public bool ShowAddress { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowTaxNumber { get; set; } = true;
    public bool ShowCommercialRegister { get; set; }
    public string? HeaderLineAr { get; set; }
    public string? HeaderLineEn { get; set; }

    // ---- document info / items / totals: keys hidden from the document's catalog
    public List<string> HiddenInfo { get; set; } = [];
    public List<string> HiddenColumns { get; set; } = [];
    public List<string> HiddenTotals { get; set; } = [];

    /// <summary>Item names: store (receipt language) | ar | en | both.</summary>
    public string ItemNames { get; set; } = "store";
    public bool ShowAmountInWords { get; set; }

    // ---- footer
    public bool ShowFooterText { get; set; } = true;
    public string? ReturnPolicyAr { get; set; }
    public string? ReturnPolicyEn { get; set; }
    public string? BankDetailsAr { get; set; }
    public string? BankDetailsEn { get; set; }
    public bool ShowSignatures { get; set; }
    public bool ShowBarcode { get; set; }
    public bool ShowDocumentNumber { get; set; } = true;

    // ---- print
    public decimal FontScale { get; set; } = 1m;

    /// <summary>compact | normal | relaxed (line spacing).</summary>
    public string Density { get; set; } = "normal";

    /// <summary>Page margin for A4 (mm).</summary>
    public int MarginMm { get; set; } = 10;

    /// <summary>A4 orientation.</summary>
    public bool Landscape { get; set; } = true;

    public string Key => PrintTemplateCatalog.KeyOf(Document, Format);
    public bool IsA4 => Format == PrintTemplateCatalog.A4;

    public bool Shows(string section) => !HiddenSections.Contains(section);
    public bool ShowsInfo(string key) => !HiddenInfo.Contains(key);
    public bool ShowsColumn(string key) => !HiddenColumns.Contains(key);
    public bool ShowsTotal(string key) => !HiddenTotals.Contains(key);

    public string? HeaderLineFor(string language) => language == "ar" ? HeaderLineAr : HeaderLineEn;
    public string? ReturnPolicyFor(string language) => language == "ar" ? ReturnPolicyAr : ReturnPolicyEn;
    public string? BankDetailsFor(string language) => language == "ar" ? BankDetailsAr : BankDetailsEn;
}

/// <summary>Which documents/sizes exist and which parts each one has (keys only; labels live in the web layer).</summary>
public static class PrintTemplateCatalog
{
    public const string Sale = "sale";
    public const string Return = "return";
    public const string Purchase = "purchase";
    public const string Shift = "shift";
    public const string Mm80 = "80mm";
    public const string A4 = "a4";

    public static readonly string[] Sections = ["header", "info", "items", "totals", "footer"];

    /// <summary>Every (document, paper size) that has a printed layout. Purchases print on A4 only.</summary>
    public static readonly (string Document, string Format)[] All =
    [
        (Sale, Mm80), (Sale, A4), (Return, Mm80), (Return, A4), (Purchase, A4), (Shift, Mm80), (Shift, A4)
    ];

    public static string KeyOf(string document, string format) => $"{document}-{format}";

    public static bool Exists(string document, string format) => All.Contains((document, format));

    public static string NormalizeFormat(string? format) =>
        string.Equals(format, "a4", StringComparison.OrdinalIgnoreCase) ? A4 : Mm80;

    public static string[] InfoKeys(string document) => document switch
    {
        Sale => ["date", "cashier", "customer", "mobile", "address", "shift", "payment", "reference"],
        Return => ["original", "date", "user", "reason", "refund"],
        Purchase => ["date", "status", "supplier", "createdBy", "confirmedBy", "notes"],
        Shift => ["opened", "openedBy", "closed", "closedBy", "participants", "invoices"],
        _ => []
    };

    public static string[] ColumnKeys(string document) => document switch
    {
        Sale => ["no", "code", "item", "unit", "qty", "catalog", "price", "discount", "total"],
        Return => ["no", "item", "unit", "qty", "price", "refund"],
        Purchase => ["no", "item", "unit", "qty", "cost", "total"],
        Shift => ["cashier", "invoices", "cash", "noncash"],
        _ => []
    };

    public static string[] TotalKeys(string document) => document switch
    {
        Sale => ["count", "subtotal", "discount", "total", "vat", "paid", "change"],
        Return => ["count", "total", "method"],
        Purchase => ["total"],
        Shift => ["methods", "totalSales", "opening", "cashSales", "refunds", "cashouts", "expected", "actual", "difference", "handover", "result"],
        _ => []
    };

    /// <summary>
    /// The layout used until someone saves one: the same documents as before Phase 24, with header and footer
    /// switches taken from the older store-wide receipt settings.
    /// </summary>
    public static PrintTemplate Default(string document, string format, StoreSettings s)
    {
        var a4 = format == A4;
        var t = new PrintTemplate
        {
            Document = document,
            Format = format,
            ShowLogo = a4 ? s.A4ShowLogo : s.ShowLogoOn80mm,
            ShowAddress = s.ShowAddressOnReceipt,
            ShowPhone = s.ShowPhoneOnReceipt,
            ShowTaxNumber = s.ShowTaxNumberOnReceipt,
            ShowCommercialRegister = a4 && s.A4ShowCommercialRegister,
            ShowBarcode = !a4 && s.ShowBarcode && document is Sale or Return,
            ShowSignatures = a4,
            ShowAmountInWords = a4 && document != Shift,
            ShowFooterText = document is Sale or Return,
            FontScale = a4 ? 1m : s.ReceiptFontScale,
            Landscape = true
        };

        // 80 mm keeps the narrow columns it always had.
        if (!a4)
        {
            t.HiddenColumns = document switch
            {
                Sale => ["no", "code", "unit", "catalog", "discount"],
                Return => ["no", "unit"],
                _ => []
            };
            t.HiddenInfo = document == Sale ? ["address", "shift", "payment", "reference"] : [];
        }
        else
        {
            // A4 return slips list item, qty, price and refund (as before); the rest is on.
            t.HiddenColumns = document == Return ? ["no", "unit"] : [];
            t.HiddenInfo = document == Return ? ["user"] : [];
        }

        return t;
    }

    /// <summary>Cleans a template coming from the designer: known keys only, every section once, ranges clamped.</summary>
    public static PrintTemplate Normalize(PrintTemplate t)
    {
        var known = Sections.ToHashSet();
        var order = t.SectionOrder.Where(known.Contains).Distinct().ToList();
        order.AddRange(Sections.Where(s => !order.Contains(s)));
        t.SectionOrder = order;
        t.HiddenSections = t.HiddenSections.Where(known.Contains).Distinct().ToList();
        t.HiddenInfo = t.HiddenInfo.Intersect(InfoKeys(t.Document)).ToList();
        t.HiddenColumns = t.HiddenColumns.Intersect(ColumnKeys(t.Document)).ToList();
        t.HiddenTotals = t.HiddenTotals.Intersect(TotalKeys(t.Document)).ToList();
        t.ItemNames = t.ItemNames is "ar" or "en" or "both" ? t.ItemNames : "store";
        t.Density = t.Density is "compact" or "relaxed" ? t.Density : "normal";
        t.FontScale = Math.Clamp(t.FontScale, 0.8m, 1.4m);
        t.MarginMm = Math.Clamp(t.MarginMm, 3, 30);
        t.HeaderLineAr = Trim(t.HeaderLineAr, 200);
        t.HeaderLineEn = Trim(t.HeaderLineEn, 200);
        t.ReturnPolicyAr = Trim(t.ReturnPolicyAr, 500);
        t.ReturnPolicyEn = Trim(t.ReturnPolicyEn, 500);
        t.BankDetailsAr = Trim(t.BankDetailsAr, 500);
        t.BankDetailsEn = Trim(t.BankDetailsEn, 500);
        return t;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var v = value.Trim();
        return v.Length > max ? v[..max] : v;
    }
}
