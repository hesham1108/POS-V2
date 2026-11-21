namespace StoresPOS.BLL.DTOs;

// Phase 23 — new reports. Ranges are UTC instants (from inclusive, to exclusive) unless noted.

// ------------------------------------------------------------------ discounts and price changes

public class DiscountAuditUserDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Invoices { get; set; }
    public int DiscountCount { get; set; }
    public decimal DiscountAmount { get; set; }
    public int OverrideLines { get; set; }

    /// <summary>(catalog price − charged price) × qty: positive = sold below the catalog price.</summary>
    public decimal OverrideAmount { get; set; }
    public int CancelledCount { get; set; }
    public int ReturnsCount { get; set; }
    public decimal ReturnsAmount { get; set; }
}

public class PriceOverrideLineDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CatalogPrice { get; set; }
    public decimal ChargedPrice { get; set; }
    public decimal Difference { get; set; }
    public string ByName { get; set; } = string.Empty;
}

public class DiscountAuditReportDto
{
    public IReadOnlyList<DiscountAuditUserDto> Users { get; set; } = [];
    public IReadOnlyList<PriceOverrideLineDto> Overrides { get; set; } = [];
    public decimal DiscountTotal => Users.Sum(x => x.DiscountAmount);
    public decimal OverrideTotal => Users.Sum(x => x.OverrideAmount);
    public int CancelledTotal => Users.Sum(x => x.CancelledCount);
    public decimal ReturnsTotal => Users.Sum(x => x.ReturnsAmount);
}

// ------------------------------------------------------------------ sales by hour and weekday

public class SalesTimeReportDto
{
    /// <summary>0–23 (store-local hour).</summary>
    public IReadOnlyList<NamedAmountDto> ByHour { get; set; } = [];

    /// <summary>Id = (int)DayOfWeek, Saturday first (Egyptian week).</summary>
    public IReadOnlyList<NamedAmountDto> ByWeekday { get; set; } = [];

    /// <summary>[weekday index in <see cref="ByWeekday"/> order][hour] = amount.</summary>
    public decimal[][] Heat { get; set; } = [];
    public int InvoiceCount => ByHour.Sum(x => x.Count);
    public decimal Total => ByHour.Sum(x => x.Amount);
}

// ------------------------------------------------------------------ product performance

public class ProductPerformanceRowDto
{
    public int ProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Sales { get; set; }
    public decimal Stock { get; set; }
    public decimal StockCostValue { get; set; }
    public DateTime? LastSoldUtc { get; set; }
    public int? DaysSinceSale { get; set; }
}

public class ProductPerformanceReportDto
{
    public IReadOnlyList<ProductPerformanceRowDto> Best { get; set; } = [];
    public IReadOnlyList<ProductPerformanceRowDto> Worst { get; set; } = [];

    /// <summary>Active products with stock and no sale in <see cref="DeadDays"/> days (as of today).</summary>
    public IReadOnlyList<ProductPerformanceRowDto> Dead { get; set; } = [];
    public int DeadDays { get; set; }
    public decimal DeadStockValue => Dead.Sum(x => x.StockCostValue);
    public int ProductsSold { get; set; }
}

// ------------------------------------------------------------------ profit (Profit.View)

public class ProfitRowDto
{
    public int? Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Sales { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Sales - Cost;
    public decimal? MarginPercent => Sales == 0 ? null : Math.Round(Profit / Sales * 100m, 1);
}

public class ProfitReportDto
{
    /// <summary>Net of invoice discounts and of returns made in the range.</summary>
    public decimal Sales { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Sales - Cost;
    public decimal? MarginPercent => Sales == 0 ? null : Math.Round(Profit / Sales * 100m, 1);
    public IReadOnlyList<ProfitRowDto> ByProduct { get; set; } = [];
    public IReadOnlyList<ProfitRowDto> ByCategory { get; set; } = [];

    /// <summary>Id unused; NameEn = yyyy-MM-dd (store-local).</summary>
    public IReadOnlyList<ProfitRowDto> ByDay { get; set; } = [];
}

// ------------------------------------------------------------------ VAT

public class TaxDayDto
{
    public DateTime Date { get; set; }
    public decimal Sales { get; set; }
    public decimal SalesVat { get; set; }
    public decimal Returns { get; set; }
    public decimal ReturnsVat { get; set; }
    public decimal NetVat => SalesVat - ReturnsVat;
}

public class TaxReportDto
{
    public decimal VatPercent { get; set; }
    public bool PricesIncludeVat { get; set; }
    public IReadOnlyList<TaxDayDto> Days { get; set; } = [];
    public decimal Sales => Days.Sum(x => x.Sales);
    public decimal SalesVat => Days.Sum(x => x.SalesVat);
    public decimal Returns => Days.Sum(x => x.Returns);
    public decimal ReturnsVat => Days.Sum(x => x.ReturnsVat);
    public decimal NetVat => SalesVat - ReturnsVat;
}

// ------------------------------------------------------------------ drawer differences per cashier

public class DrawerUserDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Shifts { get; set; }
    public int ShortageCount { get; set; }
    public decimal ShortageTotal { get; set; }
    public int SurplusCount { get; set; }
    public decimal SurplusTotal { get; set; }
    public decimal Net => SurplusTotal + ShortageTotal;

    /// <summary>Share of their shifts that ended short.</summary>
    public decimal ShortagePercent => Shifts == 0 ? 0 : Math.Round(ShortageCount * 100m / Shifts, 1);
}

public class DrawerShiftDto
{
    public int ShiftId { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public string ClosedBy { get; set; } = string.Empty;
    public IReadOnlyList<string> Participants { get; set; } = [];
    public decimal Difference { get; set; }
}

public class DrawerReportDto
{
    /// <summary>Each closed shift counts for every person who worked in it.</summary>
    public IReadOnlyList<DrawerUserDto> Users { get; set; } = [];
    public IReadOnlyList<DrawerShiftDto> Shifts { get; set; } = [];
    public decimal ShortageTotal => Shifts.Where(x => x.Difference < 0).Sum(x => x.Difference);
    public decimal SurplusTotal => Shifts.Where(x => x.Difference > 0).Sum(x => x.Difference);
}

// ------------------------------------------------------------------ cheque aging

public class ChequeAgingBucketDto
{
    /// <summary>notdue | 1-30 | 31-60 | 61-90 | 90+</summary>
    public string Key { get; set; } = string.Empty;
    public int InCount { get; set; }
    public decimal InAmount { get; set; }
    public int OutCount { get; set; }
    public decimal OutAmount { get; set; }
}

public class ChequeAgingRowDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public bool Incoming { get; set; }
    public string? PartyName { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Days past the due date (negative = not due yet).</summary>
    public int DaysOverdue { get; set; }
    public string Bucket { get; set; } = string.Empty;
}

public class ChequeAgingReportDto
{
    public DateTime AsOfLocal { get; set; }
    public IReadOnlyList<ChequeAgingBucketDto> Buckets { get; set; } = [];
    public IReadOnlyList<ChequeAgingRowDto> Rows { get; set; } = [];
    public decimal OverdueIn => Rows.Where(x => x.Incoming && x.DaysOverdue > 0).Sum(x => x.Amount);
    public decimal OverdueOut => Rows.Where(x => !x.Incoming && x.DaysOverdue > 0).Sum(x => x.Amount);
}
