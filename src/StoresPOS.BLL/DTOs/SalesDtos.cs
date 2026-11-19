using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class PosProductDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public bool IsOutOfStock => StockQuantity <= 0;
    public bool IsLowStock => !IsOutOfStock && StockQuantity <= LowStockThreshold;
}

public class PosCategoryDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
}

public class PosSearchDto
{
    public string? Term { get; set; }
    public int? CategoryId { get; set; }
    public SaleType SaleType { get; set; } = SaleType.Retail;
    public int Take { get; set; } = 60;
}

public class CartLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CatalogUnitPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsPriceOverridden { get; set; }
    public decimal LineTotal { get; set; }
}

public class CartDto
{
    public int InvoiceId { get; set; }
    public int ShiftId { get; set; }
    public SaleType SaleType { get; set; }
    public IReadOnlyList<CartLineDto> Lines { get; set; } = [];
    public decimal ItemCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public int HeldCount { get; set; }
    public bool IsEmpty => Lines.Count == 0;
}

public class HeldInvoiceDto
{
    public int Id { get; set; }
    public string CashierDisplayName { get; set; } = string.Empty;
    public DateTime HeldAtUtc { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }
    public SaleType SaleType { get; set; }
}

public class DiscountDto
{
    public decimal? Amount { get; set; }
    public decimal? Percent { get; set; }
}

public class PaymentDto
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    /// <summary>Cash tendered. Ignored for non-cash (paid = total, no split tender in v1).</summary>
    public decimal? AmountPaid { get; set; }

    public string? Reference { get; set; }
}

public class CompletedSaleDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeAmount { get; set; }
}

/// <summary>Another participant's non-empty live cart — blocks shift close until completed or held.</summary>
public class InProgressSaleDto
{
    public int InvoiceId { get; set; }
    public int CashierUserId { get; set; }
    public string CashierDisplayName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal Total { get; set; }
}

public class SalesInvoiceFilterDto
{
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? CashierUserId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? ShiftId { get; set; }
    public int Take { get; set; } = 200;
}

public class SalesInvoiceListItemDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public int ShiftId { get; set; }
    public string CashierDisplayName { get; set; } = string.Empty;
    public SaleType SaleType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }
}

public class SalesInvoiceLineDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CatalogUnitPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsPriceOverridden { get; set; }
    public string? PriceOverriddenBy { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalesInvoiceDetailsDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public int ShiftId { get; set; }
    public string CashierDisplayName { get; set; } = string.Empty;
    public SaleType SaleType { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountBy { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? PaymentReference { get; set; }
    public IReadOnlyList<SalesInvoiceLineDto> Lines { get; set; } = [];
}
