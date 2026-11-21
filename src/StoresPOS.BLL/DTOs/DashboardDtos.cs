namespace StoresPOS.BLL.DTOs;

/// <summary>Dashboard period buttons (Phase 22).</summary>
public enum DashboardPeriod
{
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
    ThisMonth
}

/// <summary>A local date range [From, To) and the range just before it, the same length.</summary>
public sealed class DashboardRange
{
    public DashboardPeriod Period { get; init; }
    public DateTime FromLocal { get; init; }
    public DateTime ToLocal { get; init; }
    public DateTime PreviousFromLocal { get; init; }
    public DateTime PreviousToLocal { get; init; }

    /// <summary>One day → hourly buckets; longer → daily buckets.</summary>
    public bool IsSingleDay => (ToLocal - FromLocal).TotalDays <= 1;
}

/// <summary>One figure with its change against the previous period and a small trend line.</summary>
public sealed class DashboardKpiDto
{
    public decimal Value { get; init; }
    public decimal Previous { get; init; }

    /// <summary>Percent change; null when the previous period was zero.</summary>
    public decimal? ChangePercent => Previous == 0m ? null : Math.Round((Value - Previous) / Math.Abs(Previous) * 100m, 1);

    public IReadOnlyList<decimal> Trend { get; init; } = [];
}

/// <summary>Top of the page: shown with the first paint (Phase 22).</summary>
public sealed class DashboardOverviewDto
{
    public DashboardRange Range { get; init; } = new();
    public DateTime TodayLocal { get; init; }

    public DashboardKpiDto NetSales { get; init; } = new();
    public DashboardKpiDto Invoices { get; init; } = new();
    public DashboardKpiDto AverageInvoice { get; init; } = new();
    public DashboardKpiDto Returns { get; init; } = new();
    public int ReturnsCount { get; init; }

    /// <summary>Expected cash of the open shift; null when no shift is open.</summary>
    public decimal? CashInDrawer { get; init; }
    public ShiftSummaryDto? OpenShift { get; init; }

    public DashboardAlertsDto Alerts { get; init; } = new();
}

public sealed class DashboardAlertsDto
{
    /// <summary>A shift opened before today is still open.</summary>
    public int? OldOpenShiftId { get; init; }
    public int ChequesOverdue { get; init; }
    public int ChequesDueToday { get; init; }
    public int OutOfStock { get; init; }

    /// <summary>Hours since the newest backup file; null = no backup found. Filled by the web layer.</summary>
    public double? HoursSinceBackup { get; set; }

    /// <summary>Days until the license ends; null = no end date. Filled by the web layer.</summary>
    public int? LicenseDaysLeft { get; set; }

    public bool BackupTooOld => HoursSinceBackup is null || HoursSinceBackup > 24;
}

/// <summary>Charts row (loaded after the page).</summary>
public sealed class DashboardChartsDto
{
    public bool Hourly { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<decimal> Current { get; init; } = [];
    public IReadOnlyList<decimal> Previous { get; init; } = [];
    public IReadOnlyList<DashboardSliceDto> PaymentMethods { get; init; } = [];
}

public sealed class DashboardSliceDto
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Count { get; init; }
}

/// <summary>Analysis + money + activity rows (loaded after the page).</summary>
public sealed class DashboardDetailsDto
{
    public IReadOnlyList<DashboardTopProductDto> TopProducts { get; init; } = [];
    public IReadOnlyList<DashboardCashierDto> Cashiers { get; init; } = [];
    public IReadOnlyList<StockBalanceDto> LowStock { get; init; } = [];
    public int LowStockCount { get; init; }
    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; init; } = [];
    public IReadOnlyList<DashboardChequeDueDto> ChequesDue { get; init; } = [];
    public IReadOnlyList<DashboardActivityDto> Activity { get; init; } = [];
}

public sealed class DashboardTopProductDto
{
    public int ProductId { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Sales { get; init; }
}

public sealed class DashboardCashierDto
{
    public int UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int Invoices { get; init; }
    public decimal Sales { get; init; }
}

public sealed class DashboardChequeDueDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public bool Incoming { get; init; }
    public string? PartyName { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Amount { get; init; }
    public int DaysLeft { get; init; }
}

public enum DashboardActivityKind
{
    Sale,
    Return,
    CashOut,
    Purchase
}

public sealed class DashboardActivityDto
{
    public DashboardActivityKind Kind { get; init; }
    public int Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string? Who { get; init; }
    public DateTime AtUtc { get; init; }

    /// <summary>Signed: money in positive, out negative (purchases shown as positive cost).</summary>
    public decimal Amount { get; init; }
}
