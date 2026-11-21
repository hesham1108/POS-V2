using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

/// <summary>Read-only reports (docs/page-composition.md §20). Date filters are UTC instants: from inclusive, to exclusive.</summary>
public interface IReportService
{
    Task<SalesReportDto> GetSalesAsync(SalesReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<PurchasesReportDto> GetPurchasesAsync(PurchasesReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<InventoryReportDto> GetInventoryAsync(InventoryReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<ShiftsReportDto> GetShiftsAsync(ShiftHistoryFilterDto filter, CancellationToken cancellationToken = default);
    Task<CashOutsReportDto> GetCashOutsAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default);

    // Phase 23 — new reports (the CashOuts filter is just a date range here; UserId is ignored)
    Task<DiscountAuditReportDto> GetDiscountAuditAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<SalesTimeReportDto> GetSalesTimeAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<ProductPerformanceReportDto> GetProductPerformanceAsync(CashOutsReportFilterDto filter, int deadDays = 90, int top = 20, CancellationToken cancellationToken = default);
    Task<ProfitReportDto> GetProfitAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<TaxReportDto> GetTaxAsync(CashOutsReportFilterDto filter, decimal vatPercent, bool pricesIncludeVat, CancellationToken cancellationToken = default);
    Task<DrawerReportDto> GetDrawerAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<ChequeAgingReportDto> GetChequeAgingAsync(CancellationToken cancellationToken = default);

    /// <summary>Users for the cashier/user filters (inactive users stay listed: they appear in history).</summary>
    Task<IReadOnlyList<NamedAmountDto>> GetUserOptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Home page for Admin/Manager (docs/page-composition.md §19).</summary>
public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);

    // Phase 22 — period dashboard
    DashboardRange GetRange(DashboardPeriod period);
    Task<DashboardOverviewDto> GetOverviewAsync(DashboardPeriod period, CancellationToken cancellationToken = default);
    Task<DashboardChartsDto> GetChartsAsync(DashboardPeriod period, CancellationToken cancellationToken = default);
    Task<DashboardDetailsDto> GetDetailsAsync(DashboardPeriod period, CancellationToken cancellationToken = default);
}
