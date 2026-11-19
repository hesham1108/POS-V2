namespace StoresPOS.DAL.Entities;

/// <summary>A cashier who opened or resumed (joined) a shift.</summary>
public class ShiftParticipant
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public Shift Shift { get; set; } = null!;
    public User User { get; set; } = null!;
}
