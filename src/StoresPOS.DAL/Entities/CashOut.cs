namespace StoresPOS.DAL.Entities;

/// <summary>
/// Cash taken out of the drawer during a shift (journey E, business rule 7): amount + reason,
/// tied to the open shift and the user. Reduces expected cash.
/// </summary>
public class CashOut
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Shift Shift { get; set; } = null!;
    public User User { get; set; } = null!;
}
