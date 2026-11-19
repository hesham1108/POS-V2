using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class StockBalanceDto
{
    public int ProductId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public string? CategoryNameEn { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
}

public class StockBalanceFilterDto
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool LowStockOnly { get; set; }
    public bool IncludeInactive { get; set; }
}

public class StockMovementDto
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public StockMovementType MovementType { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Reason { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class StockMovementFilterDto
{
    public int? ProductId { get; set; }
    public StockMovementType? MovementType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Take { get; set; } = 100;
}

public class StockAdjustmentDto
{
    public int ProductId { get; set; }

    /// <summary>Signed delta to apply. Ignored when <see cref="SetQuantityTo"/> is set.</summary>
    public decimal? QuantityChange { get; set; }

    /// <summary>Optional absolute counted quantity; delta is derived.</summary>
    public decimal? SetQuantityTo { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public class PurchaseStockLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class SaleStockLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}
