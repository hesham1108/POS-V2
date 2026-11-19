namespace StoresPOS.DAL.Entities;

public class StockMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public StockMovementType MovementType { get; set; }

    /// <summary>Signed quantity change applied to stock.</summary>
    public decimal QuantityChange { get; set; }

    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }

    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Reason { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
