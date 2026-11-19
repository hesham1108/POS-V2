namespace StoresPOS.DAL.Entities;

public class PurchaseInvoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public int? ConfirmedByUserId { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ConfirmedByUser { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}
