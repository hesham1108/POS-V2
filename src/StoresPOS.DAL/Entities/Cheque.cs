namespace StoresPOS.DAL.Entities;

/// <summary>
/// Cheque linked to exactly one customer (incoming) or one supplier (outgoing) — rule 14.
/// Money moves only on Collected / Paid — rule 15.
/// </summary>
public class Cheque
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public ChequeDirection Direction { get; set; }
    public ChequeStatus Status { get; set; } = ChequeStatus.Pending;

    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }

    public int? SalesInvoiceId { get; set; }
    public int? PurchaseInvoiceId { get; set; }

    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Treasury used when Collected or Paid (null while Pending / Bounced / Cancelled).</summary>
    public int? SettledTreasuryId { get; set; }
    public DateTime? SettledAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public Supplier? Supplier { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Treasury? SettledTreasury { get; set; }
    public ICollection<ChequeStatusHistory> StatusHistory { get; set; } = new List<ChequeStatusHistory>();
}
