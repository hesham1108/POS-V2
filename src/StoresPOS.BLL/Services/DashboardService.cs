using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class DashboardService : IDashboardService
{
    private const int RecentCount = 5;

    private readonly ApplicationDbContext _db;
    private readonly IShiftService _shifts;
    private readonly ISalesService _sales;
    private readonly ICashOutService _cashOuts;
    private readonly ITreasuryService _treasuries;
    private readonly TimeProvider _time;

    public DashboardService(
        ApplicationDbContext db,
        IShiftService shifts,
        ISalesService sales,
        ICashOutService cashOuts,
        ITreasuryService treasuries,
        TimeProvider time)
    {
        _db = db;
        _shifts = shifts;
        _sales = sales;
        _cashOuts = cashOuts;
        _treasuries = treasuries;
        _time = time;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // "Today" is the store-local calendar day (same rule as the shift gate).
        var zone = _time.LocalTimeZone;
        var todayLocal = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).Date;
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(todayLocal, DateTimeKind.Unspecified), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Unspecified), zone);

        var cashIds = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.Kind == PaymentMethodKind.Cash)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (cashIds.Count == 0)
        {
            cashIds = [(byte)PaymentMethod.Cash];
        }

        var sales = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Completed
                        && x.CompletedAtUtc >= fromUtc && x.CompletedAtUtc < toUtc)
            .Select(x => new { x.Total, Method = x.PaymentMethod!.Value })
            .ToListAsync(cancellationToken);

        var returns = await _db.SalesReturns.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
            .Select(x => x.TotalAmount)
            .ToListAsync(cancellationToken);

        var cashOutsToday = await _db.CashOuts.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var lowStock = _db.Products.AsNoTracking().Where(x => x.IsActive && x.StockQuantity <= x.LowStockThreshold);

        return new DashboardDto
        {
            TodayLocal = todayLocal,
            TodaySales = sales.Sum(x => x.Total),
            TodayCash = sales.Where(x => cashIds.Contains((byte)x.Method)).Sum(x => x.Total),
            TodayNonCash = sales.Where(x => !cashIds.Contains((byte)x.Method)).Sum(x => x.Total),
            TodayInvoiceCount = sales.Count,
            TodayReturnsCount = returns.Count,
            TodayReturnsAmount = returns.Sum(),
            TodayCashOuts = cashOutsToday,
            OpenShift = await _shifts.GetOpenShiftAsync(cancellationToken),
            LowStockCount = await lowStock.CountAsync(cancellationToken),
            LowStockTop = await lowStock
                .OrderBy(x => x.StockQuantity)
                .ThenBy(x => x.NameEn)
                .Take(RecentCount)
                .Select(x => new StockBalanceDto
                {
                    ProductId = x.Id,
                    NameAr = x.NameAr,
                    NameEn = x.NameEn,
                    Barcode = x.Barcode,
                    Unit = x.Unit,
                    StockQuantity = x.StockQuantity,
                    LowStockThreshold = x.LowStockThreshold,
                    IsLowStock = true,
                    IsActive = true
                })
                .ToListAsync(cancellationToken),
            RecentSales = await _sales.GetInvoicesAsync(new SalesInvoiceFilterDto { Take = RecentCount }, cancellationToken),
            RecentCashOuts = await _cashOuts.GetListAsync(new CashOutFilterDto { Take = RecentCount }, cancellationToken),
            RecentPurchases = await _db.PurchaseInvoices.AsNoTracking()
                .Where(x => x.Status == PurchaseInvoiceStatus.Confirmed)
                .OrderByDescending(x => x.ConfirmedAtUtc)
                .Take(RecentCount)
                .Select(x => new PurchasesReportInvoiceDto
                {
                    Id = x.Id,
                    InvoiceNumber = x.InvoiceNumber,
                    InvoiceDate = x.InvoiceDate,
                    SupplierName = x.Supplier.NameEn,
                    LineCount = x.Items.Count,
                    Total = x.SubTotal
                })
                .ToListAsync(cancellationToken),
            Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken)
        };
    }
}
