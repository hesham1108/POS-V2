using System.ComponentModel.DataAnnotations;

namespace StoresPOS.PL.ViewModels;

public class ProductFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Arabic name")]
    [StringLength(200)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [Display(Name = "English name")]
    [StringLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Display(Name = "Barcode")]
    [StringLength(64)]
    public string? Barcode { get; set; }

    [Required]
    [Display(Name = "Unit")]
    [StringLength(30)]
    public string Unit { get; set; } = "pcs";

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Purchase price (EGP)")]
    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Display(Name = "Retail markup %")]
    public decimal RetailMarkupPercent { get; set; }

    [Display(Name = "Retail price (EGP)")]
    [Range(0, double.MaxValue)]
    public decimal RetailPrice { get; set; }

    [Display(Name = "Wholesale markup %")]
    public decimal WholesaleMarkupPercent { get; set; }

    [Display(Name = "Wholesale price (EGP)")]
    [Range(0, double.MaxValue)]
    public decimal WholesalePrice { get; set; }

    [Display(Name = "Low-stock threshold")]
    [Range(0, double.MaxValue)]
    public decimal LowStockThreshold { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public decimal StockQuantity { get; set; }

    public IReadOnlyList<CategoryOptionViewModel> CategoryOptions { get; set; } = [];
}

public class ProductListItemViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
}

public class ProductIndexViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool LowStockOnly { get; set; }
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<CategoryOptionViewModel> CategoryOptions { get; set; } = [];
    public IReadOnlyList<ProductListItemViewModel> Products { get; set; } = [];
}
