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

    /// <summary>Users for the cashier/user filters (inactive users stay listed: they appear in history).</summary>
    Task<IReadOnlyList<NamedAmountDto>> GetUserOptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Home page for Admin/Manager (docs/page-composition.md §19).</summary>
public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
