namespace StoresPOS.DAL.Entities;

public class PurchaseInvoiceItem
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Snapshot of product Arabic name at purchase time.</summary>
    public string ProductNameAr { get; set; } = string.Empty;

    /// <summary>Snapshot of product English name at purchase time.</summary>
    public string ProductNameEn { get; set; } = string.Empty;

    public string Unit { get; set; } = "pcs";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
