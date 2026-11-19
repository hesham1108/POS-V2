namespace StoresPOS.DAL.Entities;

public class SalesReturnItem
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public int SalesInvoiceItemId { get; set; }
    public int ProductId { get; set; }

    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>Price charged on the original line.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Refunded amount for this line (after its share of the invoice discount).</summary>
    public decimal RefundAmount { get; set; }

    public SalesReturn SalesReturn { get; set; } = null!;
    public SalesInvoiceItem SalesInvoiceItem { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
