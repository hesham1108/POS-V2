using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.ViewModels;

public class WarehouseIndexViewModel
{
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<WarehouseListItemDto> Warehouses { get; set; } = [];
    public bool CanManage { get; set; }
    public bool CanTransfer { get; set; }
}

public class WarehouseEditViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(120)]
    [Display(Name = "English name")]
    public string NameEn { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    [Display(Name = "Arabic name")]
    public string NameAr { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Default warehouse")]
    public bool IsDefault { get; set; }
}

public class WarehouseStockViewModel
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string? Search { get; set; }
    public IReadOnlyList<WarehouseStockRowDto> Rows { get; set; } = [];
}

public class WarehouseTransferLineViewModel
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class WarehouseTransferViewModel
{
    [Display(Name = "From warehouse")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose the source warehouse.")]
    public int FromWarehouseId { get; set; }

    [Display(Name = "To warehouse")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose the destination warehouse.")]
    public int ToWarehouseId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Notes (optional)")]
    public string? Notes { get; set; }

    public List<WarehouseTransferLineViewModel> Items { get; set; } = [new()];

    public IReadOnlyList<WarehouseListItemDto> Warehouses { get; set; } = [];
    public IReadOnlyList<ProductOptionViewModel> ProductOptions { get; set; } = [];
}

public class WarehouseTransferListViewModel
{
    public int? WarehouseId { get; set; }
    public IReadOnlyList<WarehouseListItemDto> Warehouses { get; set; } = [];
    public IReadOnlyList<StockTransferListItemDto> Transfers { get; set; } = [];
    public bool CanTransfer { get; set; }
}

public class WarehouseTransferDetailViewModel
{
    public StockTransferDetailDto Transfer { get; set; } = null!;
}
