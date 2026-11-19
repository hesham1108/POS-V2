namespace StoresPOS.DAL.Entities;

public class ChequeStatusHistory
{
    public long Id { get; set; }
    public int ChequeId { get; set; }
    public ChequeStatus Status { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? TreasuryId { get; set; }
    public string? Note { get; set; }

    public Cheque Cheque { get; set; } = null!;
    public User User { get; set; } = null!;
    public Treasury? Treasury { get; set; }
}
