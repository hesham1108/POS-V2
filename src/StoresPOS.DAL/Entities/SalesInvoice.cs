namespace StoresPOS.DAL.Entities;

public class SalesInvoice
{
    public int Id { get; set; }

    /// <summary>Sequential number assigned at completion (S-000001). Null for drafts/held.</summary>
    public int? InvoiceSequence { get; set; }
    public string? InvoiceNumber { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;
    public SaleType SaleType { get; set; } = SaleType.Retail;

    public int ShiftId { get; set; }
    public int CashierUserId { get; set; }

    /// <summary>Null = walk-in customer ("عميل نقدي"). Customer records arrive in Phase 10.</summary>
    public int? CustomerId { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? DiscountByUserId { get; set; }
    public decimal Total { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string? PaymentReference { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HeldAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public byte[]? RowVersion { get; set; }

    public Shift Shift { get; set; } = null!;
    public User CashierUser { get; set; } = null!;
    public User? DiscountByUser { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}
