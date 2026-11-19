using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

/// <summary>An invoice line as the return screen sees it: what was sold, already returned and still returnable.</summary>
public class ReturnableLineDto
{
    public int SalesInvoiceItemId { get; set; }
    public int ProductId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal AvailableQuantity => Math.Max(0m, SoldQuantity - ReturnedQuantity);

    /// <summary>Price charged on the invoice line.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Refund per unit after the line's share of the invoice discount.</summary>
    public decimal RefundUnitPrice { get; set; }
}

public class ReturnableInvoiceDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public string CashierDisplayName { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal RefundedSoFar { get; set; }
    public IReadOnlyList<ReturnableLineDto> Lines { get; set; } = [];
    public IReadOnlyList<SalesReturnListItemDto> PreviousReturns { get; set; } = [];

    /// <summary>Days allowed for returns (0 = no limit).</summary>
    public int ReturnWindowDays { get; set; }
    public bool IsWithinWindow { get; set; }
    public bool HasOpenShift { get; set; }
    public bool IsFullyReturned => Lines.All(x => x.AvailableQuantity <= 0);
}

public class ReturnLineInputDto
{
    public int SalesInvoiceItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateReturnDto
{
    public int InvoiceId { get; set; }
    public IReadOnlyList<ReturnLineInputDto> Lines { get; set; } = [];
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;
    public string? Reason { get; set; }
}

public class SalesReturnFilterDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? UserId { get; set; }
    public int? ShiftId { get; set; }
    public int? InvoiceId { get; set; }

    /// <summary>Return number or original invoice number (contains).</summary>
    public string? Search { get; set; }

    public PaymentMethod? RefundMethod { get; set; }
    public int Take { get; set; } = 500;
}

public class SalesReturnListItemDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int OriginalInvoiceId { get; set; }
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int? ShiftId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public PaymentMethod RefundMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SalesReturnLineDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
}

public class SalesReturnDetailsDto : SalesReturnListItemDto
{
    public IReadOnlyList<SalesReturnLineDto> Lines { get; set; } = [];
}
