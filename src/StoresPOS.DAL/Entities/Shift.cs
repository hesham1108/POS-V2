namespace StoresPOS.DAL.Entities;

/// <summary>
/// The single store-wide shift (business rule 1). Only one row may have <see cref="ShiftStatus.Open"/>,
/// enforced by a filtered unique index.
/// </summary>
public class Shift
{
    public int Id { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public decimal OpeningAmount { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public int OpenedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public int? ClosedByUserId { get; set; }

    /// <summary>Snapshot of expected drawer cash at close.</summary>
    public decimal? ExpectedCash { get; set; }

    /// <summary>Counted cash; null = Not Reconciled (rule 4).</summary>
    public decimal? ActualCash { get; set; }

    /// <summary>ActualCash − ExpectedCash; null when not reconciled.</summary>
    public decimal? CashDifference { get; set; }

    public ShiftReconciliation? Reconciliation { get; set; }
    public string? CloseNotes { get; set; }

    /// <summary>Treasury that received the drawer handover at close (Phase 12).</summary>
    public int? HandoverTreasuryId { get; set; }

    /// <summary>Amount handed over (counted cash, or expected when not counted).</summary>
    public decimal? HandoverAmount { get; set; }

    public byte[]? RowVersion { get; set; }

    public User OpenedByUser { get; set; } = null!;
    public User? ClosedByUser { get; set; }
    public Treasury? HandoverTreasury { get; set; }
    public ICollection<ShiftParticipant> Participants { get; set; } = new List<ShiftParticipant>();
}
