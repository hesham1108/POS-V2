using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class WarehouseListItemDto
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public decimal TotalQuantity { get; set; }
    public int ProductCount { get; set; }
    public int MovementCount { get; set; }
}

public class WarehouseDetailDto : WarehouseListItemDto
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class WarehouseFormDto
{
    public int? Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class StockTransferLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class StockTransferCreateDto
{
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<StockTransferLineDto> Items { get; set; } = [];
}

public class StockTransferListItemDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int FromWarehouseId { get; set; }
    public string FromWarehouseNameEn { get; set; } = string.Empty;
    public string FromWarehouseNameAr { get; set; } = string.Empty;
    public int ToWarehouseId { get; set; }
    public string ToWarehouseNameEn { get; set; } = string.Empty;
    public string ToWarehouseNameAr { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class StockTransferItemDto
{
    public int ProductId { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public decimal Quantity { get; set; }
}

public class StockTransferDetailDto : StockTransferListItemDto
{
    public string? Notes { get; set; }
    public IReadOnlyList<StockTransferItemDto> Items { get; set; } = [];
}

public class WarehouseStockRowDto
{
    public int ProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal Quantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock => Quantity > 0 && Quantity <= LowStockThreshold;
    public bool IsOutOfStock => Quantity <= 0;
}
