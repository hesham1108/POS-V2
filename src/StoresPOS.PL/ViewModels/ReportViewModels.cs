using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class ReportOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Shared by every report: store-local dates (end date inclusive) + filter option lists.</summary>
public abstract class ReportPageViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<ReportOption> Users { get; set; } = [];

    /// <summary>The same-length range just before From–To (comparison, Phase 23).</summary>
    public DateTime PreviousFrom => From.AddDays(-((To - From).Days + 1));
    public DateTime PreviousTo => From.AddDays(-1);

    /// <summary>Show the date range + presets (false for "as of today" reports).</summary>
    public virtual bool HasDateRange => true;

    /// <summary>The report's chart (built from the same data as the tables).</summary>
    public ReportChart? Chart { get; set; }
}

/// <summary>Phase 23 reports with one DTO and only a date range.</summary>
public class AnalysisReportViewModel<T> : ReportPageViewModel
    where T : new()
{
    public T Report { get; set; } = new();
    public bool DateRange { get; set; } = true;
    public override bool HasDateRange => DateRange;
}

/// <summary>One chart on a report page (Phase 23), drawn by reports.js with Chart.js.</summary>
public sealed class ReportChart
{
    public string Title { get; init; } = string.Empty;

    /// <summary>bar | line | doughnut</summary>
    public string Type { get; init; } = "bar";
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<ReportChartSeries> Series { get; init; } = [];
    public bool Horizontal { get; init; }

    /// <summary>Time on the x axis (reversed in Arabic so time reads right-to-left).</summary>
    public bool TimeAxis { get; init; }
}

public sealed record ReportChartSeries(string Label, IReadOnlyList<decimal> Values, bool Muted = false);

/// <summary>Change against the previous period (partial _Delta).</summary>
public sealed record ReportDelta(decimal Current, decimal Previous, bool UpIsGood = true);

public class SalesReportViewModel : ReportPageViewModel
{
    /// <summary>Same filters, the period before (comparison).</summary>
    public SalesReportDto Previous { get; set; } = new();
    public bool CanSeeProfit { get; set; }
    public int? CashierId { get; set; }
    public PaymentMethod? Method { get; set; }
    public SaleType? SaleType { get; set; }
    public int? CategoryId { get; set; }

    /// <summary>0 = walk-in (no customer), 1 retail, 2 wholesale, 3 company (customer's current type).</summary>
    public int? CustomerGroup { get; set; }
    public IReadOnlyList<ReportOption> Categories { get; set; } = [];
    public SalesReportDto Report { get; set; } = new();
}

public class PurchasesReportViewModel : ReportPageViewModel
{
    public int? SupplierId { get; set; }
    public IReadOnlyList<ReportOption> Suppliers { get; set; } = [];
    public PurchasesReportDto Report { get; set; } = new();
}

public class InventoryReportViewModel : ReportPageViewModel
{
    public int? CategoryId { get; set; }
    public int? WarehouseId { get; set; }
    public bool LowOnly { get; set; }
    public IReadOnlyList<ReportOption> Categories { get; set; } = [];
    public IReadOnlyList<ReportOption> Warehouses { get; set; } = [];
    public InventoryReportDto Report { get; set; } = new();
}

public class ShiftsReportViewModel : ReportPageViewModel
{
    public int? UserId { get; set; }
    public ShiftResultFilter? Result { get; set; }
    public ShiftsReportDto Report { get; set; } = new();
}

public class CashOutsReportViewModel : ReportPageViewModel
{
    public int? UserId { get; set; }
    public CashOutsReportDto Report { get; set; } = new();
}

public class ChequesReportViewModel : ReportPageViewModel
{
    public ChequeDirection? Direction { get; set; }
    public ChequeStatus? Status { get; set; }
    public ChequesReportDto Report { get; set; } = new();
}

public class ShiftHistoryViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? UserId { get; set; }
    public ShiftResultFilter? Result { get; set; }
    public IReadOnlyList<ReportOption> Users { get; set; } = [];
    public IReadOnlyList<ShiftHistoryItemDto> Shifts { get; set; } = [];
}

public class ShiftDetailsViewModel
{
    public ShiftSummaryDto Summary { get; set; } = null!;
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices { get; set; } = [];
    public IReadOnlyList<CashOutListItemDto> CashOuts { get; set; } = [];
    public IReadOnlyList<SalesReturnListItemDto> Returns { get; set; } = [];
}
