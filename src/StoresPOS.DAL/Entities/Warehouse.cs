namespace StoresPOS.DAL.Entities;

/// <summary>Stock location (rule 17). Product total stock = sum of <see cref="WarehouseStock"/> rows.</summary>
public class Warehouse
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<WarehouseStock> Stocks { get; set; } = new List<WarehouseStock>();
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}
