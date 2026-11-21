namespace StoresPOS.DAL.Entities;

public class Product
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public int? CategoryId { get; set; }

    public decimal PurchasePrice { get; set; }

    /// <summary>
    /// Weighted average cost of the stock on hand (Phase 23, plan §9 default for profit). Updated on every purchase
    /// receipt; 0 = not known yet (then <see cref="PurchasePrice"/> is used). Sell prices still follow the last purchase price.
    /// </summary>
    public decimal AverageCost { get; set; }
    public decimal RetailMarkupPercent { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal WholesaleMarkupPercent { get; set; }
    public decimal WholesalePrice { get; set; }

    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Category? Category { get; set; }
}
