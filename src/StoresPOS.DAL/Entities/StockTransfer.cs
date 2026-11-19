namespace StoresPOS.DAL.Entities;

/// <summary>One document moving stock between warehouses (rule 17 / journey M).</summary>
public class StockTransfer
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
