namespace StoresPOS.BLL.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public int? CategoryId { get; set; }
    public string? CategoryNameAr { get; set; }
    public string? CategoryNameEn { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal RetailMarkupPercent { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal WholesaleMarkupPercent { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
}

public class ProductListFilterDto
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool LowStockOnly { get; set; }
}

public class ProductUpsertDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public int? CategoryId { get; set; }
    public decimal PurchasePrice { get; set; }

    /// <summary>When set, retail markup is derived from this price.</summary>
    public decimal? RetailPrice { get; set; }
    public decimal? RetailMarkupPercent { get; set; }

    /// <summary>When set, wholesale markup is derived from this price.</summary>
    public decimal? WholesalePrice { get; set; }
    public decimal? WholesaleMarkupPercent { get; set; }

    public decimal LowStockThreshold { get; set; }
    public bool IsActive { get; set; } = true;
}
