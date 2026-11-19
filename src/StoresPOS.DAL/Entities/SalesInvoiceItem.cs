namespace StoresPOS.DAL.Entities;

/// <summary>
/// Snapshot line: names, catalog price and cost are copied at sale time so history never changes (rule 9).
/// A price override touches this line only; both prices + the user are kept (rule 6).
/// </summary>
public class SalesInvoiceItem
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public int ProductId { get; set; }

    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>Catalog price for the invoice's sale type when the line was priced.</summary>
    public decimal CatalogUnitPrice { get; set; }

    /// <summary>Price actually charged.</summary>
    public decimal UnitPrice { get; set; }

    public bool IsPriceOverridden { get; set; }
    public int? PriceOverriddenByUserId { get; set; }

    /// <summary>Purchase price snapshot (profit reports, Phase 10).</summary>
    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }

    public SalesInvoice SalesInvoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public User? PriceOverriddenByUser { get; set; }
}
