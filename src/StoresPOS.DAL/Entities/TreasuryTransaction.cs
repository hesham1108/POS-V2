namespace StoresPOS.DAL.Entities;

/// <summary>Ledger row for a treasury. Never edit balances silently — always write a row (rule 16).</summary>
public class TreasuryTransaction
{
    public long Id { get; set; }
    public int TreasuryId { get; set; }
    public TreasuryTransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public TreasuryTransactionSourceType SourceType { get; set; }
    public int? SourceId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    /// <summary>Pairs the two legs of a transfer (same TransferGroupId).</summary>
    public Guid? TransferGroupId { get; set; }

    public Treasury Treasury { get; set; } = null!;
    public User User { get; set; } = null!;
}
