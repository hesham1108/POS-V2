using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

/// <summary>Common date range: <see cref="FromUtc"/> inclusive, <see cref="ToUtc"/> exclusive.</summary>
public abstract class ReportFilterBaseDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public class NamedAmountDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int Count { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

// ------------------------------------------------------------------ sales

public class SalesReportFilterDto : ReportFilterBaseDto
{
    public int? CashierUserId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public SaleType? SaleType { get; set; }

    /// <summary>Includes sub-categories. Only matching lines are counted.</summary>
    public int? CategoryId { get; set; }
}

public class PaymentMethodAmountDto
{
    public PaymentMethod Method { get; set; }
    public byte MethodId => (byte)Method;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsCash { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }

    public string NameFor(string language) =>
        language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? NameAr : NameEn;
}

public class DailyAmountDto
{
    /// <summary>Store-local date.</summary>
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal Profit { get; set; }
}

public class SalesReportDto
{
    public int InvoiceCount { get; set; }

    /// <summary>Lines at charged prices, before the invoice discount.</summary>
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal NetSales { get; set; }

    public int ReturnsCount { get; set; }
    public decimal ReturnsAmount { get; set; }
    public decimal NetAfterReturns => NetSales - ReturnsAmount;

    /// <summary>Purchase price snapshot at sale time, less the cost of returned items.</summary>
    public decimal Cost { get; set; }
    public decimal Profit => NetAfterReturns - Cost;
    public decimal? MarginPercent => NetAfterReturns == 0 ? null : Math.Round(Profit / NetAfterReturns * 100m, 1);

    public decimal AverageInvoice => InvoiceCount == 0 ? 0 : Math.Round(NetSales / InvoiceCount, 2);

    public IReadOnlyList<PaymentMethodAmountDto> ByMethod { get; set; } = [];
    public IReadOnlyList<NamedAmountDto> ByCashier { get; set; } = [];
    public IReadOnlyList<NamedAmountDto> BySaleType { get; set; } = [];
    public IReadOnlyList<DailyAmountDto> ByDay { get; set; } = [];
    public IReadOnlyList<TopProductDto> TopProducts { get; set; } = [];
}

// ------------------------------------------------------------------ purchases

public class PurchasesReportFilterDto : ReportFilterBaseDto
{
    public int? SupplierId { get; set; }
}

public class PurchasesReportInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal Total { get; set; }
}

public class PurchasesReportDto
{
    public int InvoiceCount { get; set; }
    public decimal Total { get; set; }
    public IReadOnlyList<NamedAmountDto> BySupplier { get; set; } = [];
    public IReadOnlyList<NamedAmountDto> ByProduct { get; set; } = [];
    public IReadOnlyList<PurchasesReportInvoiceDto> Invoices { get; set; } = [];
}

// ------------------------------------------------------------------ inventory

public class InventoryReportFilterDto : ReportFilterBaseDto
{
    public int? CategoryId { get; set; }
    public int? WarehouseId { get; set; }
    public bool LowStockOnly { get; set; }
}

public class InventoryReportRowDto
{
    public int ProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public string? CategoryNameAr { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public decimal UnitCost { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal CostValue { get; set; }
    public decimal RetailValue { get; set; }
    public bool IsLowStock => StockQuantity > 0 && StockQuantity <= LowStockThreshold;
    public bool IsOutOfStock => StockQuantity <= 0;
}

public class MovementSummaryDto
{
    public StockMovementType Type { get; set; }
    public int Count { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
}

public class InventoryReportDto
{
    public IReadOnlyList<InventoryReportRowDto> Rows { get; set; } = [];
    public int ProductCount => Rows.Count;
    public int LowStockCount => Rows.Count(x => x.IsLowStock);
    public int OutOfStockCount => Rows.Count(x => x.IsOutOfStock);
    public decimal TotalCostValue => Rows.Sum(x => x.CostValue);
    public decimal TotalRetailValue => Rows.Sum(x => x.RetailValue);

    /// <summary>Stock movements of the listed products in the date range.</summary>
    public IReadOnlyList<MovementSummaryDto> Movements { get; set; } = [];
}

// ------------------------------------------------------------------ shifts

public class ShiftsReportDto
{
    public IReadOnlyList<ShiftHistoryItemDto> Shifts { get; set; } = [];
    public int ShiftCount => Shifts.Count;
    public int MatchedCount => Shifts.Count(x => x.Reconciliation == ShiftReconciliation.Matched);
    public int NotReconciledCount => Shifts.Count(x => x.Reconciliation == ShiftReconciliation.NotReconciled);
    public decimal SurplusTotal => Shifts.Where(x => x.CashDifference > 0).Sum(x => x.CashDifference!.Value);
    public decimal ShortageTotal => Shifts.Where(x => x.CashDifference < 0).Sum(x => x.CashDifference!.Value);
    public decimal NetDifference => SurplusTotal + ShortageTotal;
    public decimal SalesTotal => Shifts.Sum(x => x.SalesTotal);

    /// <summary>Completed sales of these shifts per cashier.</summary>
    public IReadOnlyList<NamedAmountDto> ByCashier { get; set; } = [];
}

// ------------------------------------------------------------------ cash-outs

public class CashOutsReportFilterDto : ReportFilterBaseDto
{
    public int? UserId { get; set; }
}

public class CashOutsReportDto
{
    public IReadOnlyList<CashOutListItemDto> Items { get; set; } = [];
    public decimal Total => Items.Sum(x => x.Amount);
    public IReadOnlyList<NamedAmountDto> ByUser { get; set; } = [];
    public IReadOnlyList<NamedAmountDto> ByReason { get; set; } = [];
}

// ------------------------------------------------------------------ dashboard

public class DashboardDto
{
    public DateTime TodayLocal { get; set; }

    public decimal TodaySales { get; set; }
    public decimal TodayCash { get; set; }
    public decimal TodayNonCash { get; set; }
    public int TodayInvoiceCount { get; set; }
    public int TodayReturnsCount { get; set; }
    public decimal TodayReturnsAmount { get; set; }
    public decimal TodayCashOuts { get; set; }

    /// <summary>Null when no shift is open.</summary>
    public ShiftSummaryDto? OpenShift { get; set; }

    public int LowStockCount { get; set; }
    public IReadOnlyList<StockBalanceDto> LowStockTop { get; set; } = [];

    public IReadOnlyList<SalesInvoiceListItemDto> RecentSales { get; set; } = [];
    public IReadOnlyList<CashOutListItemDto> RecentCashOuts { get; set; } = [];
    public IReadOnlyList<PurchasesReportInvoiceDto> RecentPurchases { get; set; } = [];

    /// <summary>Active treasury balances for the dashboard card (Phase 12).</summary>
    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];

    /// <summary>Pending cheque due / overdue counts (Phase 13).</summary>
    public ChequeDashboardDto Cheques { get; set; } = new();
}
