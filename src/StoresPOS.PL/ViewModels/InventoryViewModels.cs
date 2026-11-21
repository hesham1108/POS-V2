using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class InventoryIndexViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? WarehouseId { get; set; }
    public bool LowStockOnly { get; set; }
    public IReadOnlyList<CategoryOptionViewModel> CategoryOptions { get; set; } = [];
    public IReadOnlyList<WarehouseListItemDto> WarehouseOptions { get; set; } = [];
    public IReadOnlyList<StockBalanceRowViewModel> Balances { get; set; } = [];
    public int LowStockCount { get; set; }
}

public class StockBalanceRowViewModel
{
    public int ProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? WarehouseName { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
}

public class StockMovementsIndexViewModel
{
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public StockMovementType? MovementType { get; set; }
    public IReadOnlyList<ProductOptionViewModel> ProductOptions { get; set; } = [];
    public IReadOnlyList<WarehouseListItemDto> WarehouseOptions { get; set; } = [];
    public IReadOnlyList<StockMovementRowViewModel> Movements { get; set; } = [];
    public string? Search { get; set; }
    /// <summary>Paging/sort state of the list (Phase 20).</summary>
    public IPagedResult Page { get; set; } = new PagedResult<object>();
}

public class StockMovementRowViewModel
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? WarehouseName { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
}

public class StockAdjustmentFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Product is required.")]
    [Display(Name = "Product")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Warehouse is required.")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Display(Name = "Mode")]
    public string Mode { get; set; } = "delta";

    [Display(Name = "Quantity change (+/−)")]
    public decimal? QuantityChange { get; set; }

    [Display(Name = "Counted quantity")]
    [Range(0, double.MaxValue)]
    public decimal? SetQuantityTo { get; set; }

    [Required]
    [StringLength(500)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<ProductOptionViewModel> ProductOptions { get; set; } = [];
    public IReadOnlyList<WarehouseListItemDto> WarehouseOptions { get; set; } = [];
}
