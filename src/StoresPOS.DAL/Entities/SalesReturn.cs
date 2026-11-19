namespace StoresPOS.DAL.Entities;

/// <summary>
/// Customer return against a completed sales invoice (journey G). Lines are refunded at the prices the
/// original invoice charged (rule 9). A cash refund leaves the drawer of the shift it was made in.
/// </summary>
public class SalesReturn
{
    public int Id { get; set; }

    /// <summary>Sequential number R-000001.</summary>
    public int ReturnSequence { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;

    public int OriginalSalesInvoiceId { get; set; }

    /// <summary>Open shift when the return was made. Always set for cash refunds; null only for a non-cash refund with no open shift.</summary>
    public int? ShiftId { get; set; }

    public int UserId { get; set; }

    /// <summary>Cash (from the drawer) or the original invoice's non-cash method (recorded only).</summary>
    public PaymentMethod RefundMethod { get; set; }

    public decimal TotalAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SalesInvoice OriginalSalesInvoice { get; set; } = null!;
    public Shift? Shift { get; set; }
    public User User { get; set; } = null!;
    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}
