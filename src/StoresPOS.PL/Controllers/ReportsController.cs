using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Reports (docs/page-composition.md §20): filters + tables + totals + browser print (A4).
/// Dates are store-local calendar days; the "to" day is inclusive. Default range: this month so far.
/// </summary>
[Authorize(Policy = AppPermissions.ReportsView)]
public class ReportsController : Controller
{
    /// <summary>Guards against accidental multi-year scans on the store PC.</summary>
    private const int MaxRangeDays = 366;

    private readonly IReportService _reports;
    private readonly IChequeService _cheques;
    private readonly ICategoryService _categories;
    private readonly ISupplierService _suppliers;
    private readonly IWarehouseService _warehouses;
    private readonly TimeProvider _time;

    public ReportsController(
        IReportService reports,
        IChequeService cheques,
        ICategoryService categories,
        ISupplierService suppliers,
        IWarehouseService warehouses,
        TimeProvider time)
    {
        _reports = reports;
        _cheques = cheques;
        _categories = categories;
        _suppliers = suppliers;
        _warehouses = warehouses;
        _time = time;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Sales));

    [HttpGet]
    public async Task<IActionResult> Sales(
        DateTime? from,
        DateTime? to,
        int? cashierId,
        PaymentMethod? method,
        SaleType? saleType,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new SalesReportViewModel
        {
            From = f,
            To = t,
            CashierId = cashierId,
            Method = method,
            SaleType = saleType,
            CategoryId = categoryId,
            Users = await UsersAsync(cancellationToken),
            Categories = await CategoriesAsync(cancellationToken),
            Report = await _reports.GetSalesAsync(new SalesReportFilterDto
            {
                FromUtc = ToUtc(f),
                ToUtc = ToUtc(t.AddDays(1)),
                CashierUserId = cashierId,
                PaymentMethod = method,
                SaleType = saleType,
                CategoryId = categoryId
            }, cancellationToken)
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Purchases(DateTime? from, DateTime? to, int? supplierId, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var suppliers = await _suppliers.GetListAsync(new SupplierListFilterDto { IsActive = null }, cancellationToken);
        return View(new PurchasesReportViewModel
        {
            From = f,
            To = t,
            SupplierId = supplierId,
            Suppliers = suppliers.Select(x => new ReportOption { Id = x.Id, Name = x.NameEn }).ToList(),
            // Purchase invoice dates are calendar dates (no time zone), so the range is used as-is.
            Report = await _reports.GetPurchasesAsync(new PurchasesReportFilterDto
            {
                FromUtc = f,
                ToUtc = t.AddDays(1),
                SupplierId = supplierId
            }, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Inventory(
        DateTime? from,
        DateTime? to,
        int? categoryId,
        int? warehouseId,
        bool lowOnly = false,
        CancellationToken cancellationToken = default)
    {
        var (f, t) = Range(from, to);
        var warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
        return View(new InventoryReportViewModel
        {
            From = f,
            To = t,
            CategoryId = categoryId,
            WarehouseId = warehouseId,
            LowOnly = lowOnly,
            Categories = await CategoriesAsync(cancellationToken),
            Warehouses = warehouses.Select(w => new ReportOption { Id = w.Id, Name = $"{w.NameEn} / {w.NameAr}" }).ToList(),
            Report = await _reports.GetInventoryAsync(new InventoryReportFilterDto
            {
                FromUtc = ToUtc(f),
                ToUtc = ToUtc(t.AddDays(1)),
                CategoryId = categoryId,
                WarehouseId = warehouseId,
                LowStockOnly = lowOnly
            }, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Shifts(
        DateTime? from,
        DateTime? to,
        int? userId,
        ShiftResultFilter? result,
        CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        return View(new ShiftsReportViewModel
        {
            From = f,
            To = t,
            UserId = userId,
            Result = result,
            Users = await UsersAsync(cancellationToken),
            Report = await _reports.GetShiftsAsync(new ShiftHistoryFilterDto
            {
                FromUtc = ToUtc(f),
                ToUtc = ToUtc(t.AddDays(1)),
                UserId = userId,
                Result = result
            }, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CashOuts(DateTime? from, DateTime? to, int? userId, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        return View(new CashOutsReportViewModel
        {
            From = f,
            To = t,
            UserId = userId,
            Users = await UsersAsync(cancellationToken),
            Report = await _reports.GetCashOutsAsync(new CashOutsReportFilterDto
            {
                FromUtc = ToUtc(f),
                ToUtc = ToUtc(t.AddDays(1)),
                UserId = userId
            }, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Cheques(
        DateTime? from,
        DateTime? to,
        ChequeDirection? direction,
        ChequeStatus? status,
        CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        return View(new ChequesReportViewModel
        {
            From = f,
            To = t,
            Direction = direction,
            Status = status,
            Report = await _cheques.GetReportAsync(new ChequesReportFilterDto
            {
                DueFrom = f,
                DueTo = t,
                Direction = direction,
                Status = status
            }, cancellationToken)
        });
    }

    // ------------------------------------------------------------------ helpers

    private DateTime TodayLocal => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).Date;

    /// <summary>Defaults to the 1st of this month → today; swaps a reversed range; caps the length.</summary>
    private (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var today = TodayLocal;
        var t = (to ?? today).Date;
        var f = (from ?? new DateTime(t.Year, t.Month, 1)).Date;
        if (f > t)
        {
            (f, t) = (t, f);
        }

        if ((t - f).TotalDays > MaxRangeDays)
        {
            f = t.AddDays(-MaxRangeDays);
        }

        return (f, t);
    }

    private DateTime ToUtc(DateTime localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), _time.LocalTimeZone);

    private async Task<IReadOnlyList<ReportOption>> UsersAsync(CancellationToken cancellationToken) =>
        (await _reports.GetUserOptionsAsync(cancellationToken))
            .Select(x => new ReportOption { Id = x.Id ?? 0, Name = x.Name })
            .ToList();

    private async Task<IReadOnlyList<ReportOption>> CategoriesAsync(CancellationToken cancellationToken)
    {
        var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var all = await _categories.GetAllAsync(includeInactive: false, cancellationToken);
        return all
            .OrderBy(x => isAr ? (x.ParentNameAr ?? x.NameAr) : (x.ParentNameEn ?? x.NameEn))
            .ThenBy(x => x.ParentCategoryId.HasValue)
            .ThenBy(x => isAr ? x.NameAr : x.NameEn)
            .Select(x => new ReportOption
            {
                Id = x.Id,
                Name = x.ParentCategoryId.HasValue
                    ? (isAr ? $"{x.ParentNameAr} › {x.NameAr}" : $"{x.ParentNameEn} › {x.NameEn}")
                    : (isAr ? x.NameAr : x.NameEn)
            })
            .ToList();
    }
}
