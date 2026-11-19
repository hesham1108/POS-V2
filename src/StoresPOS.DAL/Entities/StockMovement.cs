namespace StoresPOS.DAL.Entities;

public class StockMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public StockMovementType MovementType { get; set; }

    /// <summary>Signed quantity change applied to this warehouse's stock.</summary>
    public decimal QuantityChange { get; set; }

    /// <summary>Warehouse quantity before this movement.</summary>
    public decimal QuantityBefore { get; set; }

    /// <summary>Warehouse quantity after this movement.</summary>
    public decimal QuantityAfter { get; set; }

    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Reason { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
