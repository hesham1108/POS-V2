namespace StoresPOS.DAL.Entities;

/// <summary>Money location outside the shift drawer (rule 16). Balance = OpeningBalance + ledger.</summary>
public class Treasury
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public TreasuryType Type { get; set; } = TreasuryType.Cash;
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<TreasuryTransaction> Transactions { get; set; } = new List<TreasuryTransaction>();
}
