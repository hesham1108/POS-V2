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
    private readonly IChequeService _cheques;
    private readonly TimeProvider _time;

    public DashboardService(
        ApplicationDbContext db,
        IShiftService shifts,
        ISalesService sales,
        ICashOutService cashOuts,
        ITreasuryService treasuries,
        IChequeService cheques,
        TimeProvider time)
    {
        _db = db;
        _shifts = shifts;
        _sales = sales;
        _cashOuts = cashOuts;
        _treasuries = treasuries;
        _cheques = cheques;
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
            Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken),
            Cheques = await _cheques.GetDashboardAsync(cancellationToken)
        };
    }

    // ================================================================== Phase 22

    private DateTime TodayLocal => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).Date;

    private DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _time.LocalTimeZone);

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _time.LocalTimeZone);

    /// <summary>Local date range for a period button and the same-length range just before it.</summary>
    public DashboardRange GetRange(DashboardPeriod period)
    {
        var today = TodayLocal;
        DateTime from, to;
        switch (period)
        {
            case DashboardPeriod.Yesterday:
                from = today.AddDays(-1); to = today; break;
            case DashboardPeriod.Last7Days:
                from = today.AddDays(-6); to = today.AddDays(1); break;
            case DashboardPeriod.Last30Days:
                from = today.AddDays(-29); to = today.AddDays(1); break;
            case DashboardPeriod.ThisMonth:
                from = new DateTime(today.Year, today.Month, 1); to = today.AddDays(1); break;
            default:
                from = today; to = today.AddDays(1); break;
        }

        DateTime prevFrom, prevTo;
        if (period == DashboardPeriod.ThisMonth)
        {
            // Same days of the previous month (1st → same day number), never past its end.
            prevFrom = from.AddMonths(-1);
            prevTo = prevFrom.AddDays((to - from).Days);
            if (prevTo > from)
            {
                prevTo = from;
            }
        }
        else
        {
            var length = to - from;
            prevFrom = from - length;
            prevTo = from;
        }

        return new DashboardRange { Period = period, FromLocal = from, ToLocal = to, PreviousFromLocal = prevFrom, PreviousToLocal = prevTo };
    }

    private sealed record SaleRow(DateTime AtUtc, decimal Total, PaymentMethod Method, int CashierUserId);

    private sealed record ReturnRow(DateTime AtUtc, decimal Amount);

    private async Task<(List<SaleRow> Sales, List<ReturnRow> Returns)> LoadAsync(DashboardRange range, CancellationToken cancellationToken)
    {
        var fromUtc = ToUtc(range.PreviousFromLocal < range.FromLocal ? range.PreviousFromLocal : range.FromLocal);
        var toUtc = ToUtc(range.ToLocal);
        var sales = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Completed && x.CompletedAtUtc >= fromUtc && x.CompletedAtUtc < toUtc)
            .Select(x => new SaleRow(x.CompletedAtUtc!.Value, x.Total, x.PaymentMethod!.Value, x.CashierUserId))
            .ToListAsync(cancellationToken);
        var returns = await _db.SalesReturns.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
            .Select(x => new ReturnRow(x.CreatedAtUtc, x.TotalAmount))
            .ToListAsync(cancellationToken);
        return (sales, returns);
    }

    private bool In(DateTime utc, DateTime fromLocal, DateTime toLocal)
    {
        var local = ToLocal(utc);
        return local >= fromLocal && local < toLocal;
    }

    /// <summary>Bucket index of a time inside a range: hour of day, or day number.</summary>
    private int Bucket(DateTime utc, DateTime fromLocal, bool hourly)
    {
        var local = ToLocal(utc);
        return hourly ? local.Hour : (local.Date - fromLocal).Days;
    }

    private (decimal[] Current, decimal[] Previous) NetByBucket(
        DashboardRange range, List<SaleRow> sales, List<ReturnRow> returns, int buckets)
    {
        var hourly = range.IsSingleDay;
        var current = new decimal[buckets];
        var previous = new decimal[buckets];
        void Add(DateTime at, decimal amount)
        {
            if (In(at, range.FromLocal, range.ToLocal))
            {
                var b = Bucket(at, range.FromLocal, hourly);
                if (b >= 0 && b < buckets) current[b] += amount;
            }
            else if (In(at, range.PreviousFromLocal, range.PreviousToLocal))
            {
                var b = Bucket(at, range.PreviousFromLocal, hourly);
                if (b >= 0 && b < buckets) previous[b] += amount;
            }
        }

        foreach (var s in sales) Add(s.AtUtc, s.Total);
        foreach (var r in returns) Add(r.AtUtc, -r.Amount);
        return (current, previous);
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
    {
        var range = GetRange(period);
        var (sales, returns) = await LoadAsync(range, cancellationToken);
        var cur = sales.Where(x => In(x.AtUtc, range.FromLocal, range.ToLocal)).ToList();
        var prev = sales.Where(x => In(x.AtUtc, range.PreviousFromLocal, range.PreviousToLocal)).ToList();
        var curRet = returns.Where(x => In(x.AtUtc, range.FromLocal, range.ToLocal)).ToList();
        var prevRet = returns.Where(x => In(x.AtUtc, range.PreviousFromLocal, range.PreviousToLocal)).ToList();

        var buckets = range.IsSingleDay ? 24 : Math.Max(1, (range.ToLocal - range.FromLocal).Days);
        var (trend, _) = NetByBucket(range, sales, returns, buckets);

        decimal Avg(List<SaleRow> rows) => rows.Count == 0 ? 0m : Math.Round(rows.Sum(x => x.Total) / rows.Count, 2);

        var shift = await _shifts.GetOpenShiftAsync(cancellationToken);
        var cheques = await _cheques.GetDashboardAsync(cancellationToken);

        return new DashboardOverviewDto
        {
            Range = range,
            TodayLocal = TodayLocal,
            NetSales = new DashboardKpiDto
            {
                Value = cur.Sum(x => x.Total) - curRet.Sum(x => x.Amount),
                Previous = prev.Sum(x => x.Total) - prevRet.Sum(x => x.Amount),
                Trend = trend
            },
            Invoices = new DashboardKpiDto
            {
                Value = cur.Count,
                Previous = prev.Count,
                Trend = CountTrend(range, cur, buckets)
            },
            AverageInvoice = new DashboardKpiDto { Value = Avg(cur), Previous = Avg(prev) },
            Returns = new DashboardKpiDto { Value = curRet.Sum(x => x.Amount), Previous = prevRet.Sum(x => x.Amount) },
            ReturnsCount = curRet.Count,
            CashInDrawer = shift?.ExpectedCash,
            OpenShift = shift,
            Alerts = new DashboardAlertsDto
            {
                OldOpenShiftId = shift is { IsOld: true } ? shift.Id : null,
                ChequesOverdue = cheques.IncomingOverdue + cheques.OutgoingOverdue,
                ChequesDueToday = cheques.IncomingDueToday + cheques.OutgoingDueToday,
                OutOfStock = await _db.Products.AsNoTracking().CountAsync(x => x.IsActive && x.StockQuantity <= 0, cancellationToken)
            }
        };
    }

    private IReadOnlyList<decimal> CountTrend(DashboardRange range, List<SaleRow> current, int buckets)
    {
        var counts = new decimal[buckets];
        foreach (var s in current)
        {
            var b = Bucket(s.AtUtc, range.FromLocal, range.IsSingleDay);
            if (b >= 0 && b < buckets) counts[b]++;
        }

        return counts;
    }

    public async Task<DashboardChartsDto> GetChartsAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
    {
        var range = GetRange(period);
        var (sales, returns) = await LoadAsync(range, cancellationToken);
        var hourly = range.IsSingleDay;
        var buckets = hourly ? 24 : Math.Max(1, (range.ToLocal - range.FromLocal).Days);
        var (current, previous) = NetByBucket(range, sales, returns, buckets);

        IEnumerable<int> indexes = Enumerable.Range(0, buckets);
        if (hourly)
        {
            // Only the hours the store works: first to last hour with sales in either day (default 9–22).
            var used = indexes.Where(i => current[i] != 0 || previous[i] != 0).ToList();
            var first = used.Count == 0 ? 9 : Math.Min(used.Min(), 9);
            var last = used.Count == 0 ? 22 : Math.Max(used.Max(), 21);
            indexes = Enumerable.Range(first, last - first + 1);
        }

        var list = indexes.ToList();
        var methods = await _db.PaymentMethods.AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);
        var slices = sales
            .Where(x => In(x.AtUtc, range.FromLocal, range.ToLocal))
            .GroupBy(x => x.Method)
            .Select(g => new DashboardSliceDto
            {
                NameEn = methods.TryGetValue((byte)g.Key, out var m) ? m.NameEn : g.Key.ToString(),
                NameAr = methods.TryGetValue((byte)g.Key, out var m2) ? m2.NameAr : g.Key.ToString(),
                Amount = g.Sum(x => x.Total),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return new DashboardChartsDto
        {
            Hourly = hourly,
            Labels = list.Select(i => hourly ? i.ToString("00") : range.FromLocal.AddDays(i).ToString("MM/dd")).ToList(),
            Current = list.Select(i => current[i]).ToList(),
            Previous = list.Select(i => previous[i]).ToList(),
            PaymentMethods = slices
        };
    }

    public async Task<DashboardDetailsDto> GetDetailsAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
    {
        var range = GetRange(period);
        var fromUtc = ToUtc(range.FromLocal);
        var toUtc = ToUtc(range.ToLocal);
        var today = TodayLocal;

        var top = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(x => x.SalesInvoice.Status == SalesInvoiceStatus.Completed
                        && x.SalesInvoice.CompletedAtUtc >= fromUtc && x.SalesInvoice.CompletedAtUtc < toUtc)
            .GroupBy(x => x.ProductId)
            .Select(g => new DashboardTopProductDto
            {
                ProductId = g.Key,
                NameEn = g.Max(x => x.ProductNameEn),
                NameAr = g.Max(x => x.ProductNameAr),
                Quantity = g.Sum(x => x.Quantity),
                Sales = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Sales)
            .Take(5)
            .ToListAsync(cancellationToken);

        var cashierRows = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Completed && x.CompletedAtUtc >= fromUtc && x.CompletedAtUtc < toUtc)
            .GroupBy(x => x.CashierUserId)
            .Select(g => new { UserId = g.Key, Invoices = g.Count(), Sales = g.Sum(x => x.Total) })
            .ToListAsync(cancellationToken);
        var ids = cashierRows.Select(x => x.UserId).ToList();
        var names = await _db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var lowStock = _db.Products.AsNoTracking().Where(x => x.IsActive && x.StockQuantity <= x.LowStockThreshold);

        var weekEnd = today.AddDays(8);
        var chequeRows = await _db.Cheques.AsNoTracking()
            .Where(x => x.Status == ChequeStatus.Pending && x.DueDate >= today && x.DueDate < weekEnd)
            .OrderBy(x => x.DueDate)
            .Take(6)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Direction,
                Party = x.Customer != null ? x.Customer.Name : x.Supplier != null ? x.Supplier.NameEn : null,
                x.DueDate,
                x.Amount
            })
            .ToListAsync(cancellationToken);

        return new DashboardDetailsDto
        {
            TopProducts = top,
            Cashiers = cashierRows
                .OrderByDescending(x => x.Sales)
                .Select(x => new DashboardCashierDto
                {
                    UserId = x.UserId,
                    DisplayName = names.GetValueOrDefault(x.UserId, "?"),
                    Invoices = x.Invoices,
                    Sales = x.Sales
                })
                .ToList(),
            LowStockCount = await lowStock.CountAsync(cancellationToken),
            LowStock = await lowStock
                .OrderBy(x => x.StockQuantity).ThenBy(x => x.NameEn)
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
            Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken),
            ChequesDue = chequeRows.Select(x => new DashboardChequeDueDto
            {
                Id = x.Id,
                Number = x.Number,
                Incoming = x.Direction == ChequeDirection.In,
                PartyName = x.Party,
                DueDate = x.DueDate,
                Amount = x.Amount,
                DaysLeft = (x.DueDate.Date - today).Days
            }).ToList(),
            Activity = await GetActivityAsync(10, cancellationToken)
        };
    }

    /// <summary>Latest sales, returns, cash-outs and confirmed purchases merged into one timeline.</summary>
    private async Task<IReadOnlyList<DashboardActivityDto>> GetActivityAsync(int take, CancellationToken cancellationToken)
    {
        var sales = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Completed)
            .OrderByDescending(x => x.CompletedAtUtc).Take(take)
            .Select(x => new DashboardActivityDto
            {
                Kind = DashboardActivityKind.Sale, Id = x.Id, Reference = x.InvoiceNumber!,
                Who = x.CashierUser.DisplayName, AtUtc = x.CompletedAtUtc!.Value, Amount = x.Total
            }).ToListAsync(cancellationToken);
        var returns = await _db.SalesReturns.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).Take(take)
            .Select(x => new DashboardActivityDto
            {
                Kind = DashboardActivityKind.Return, Id = x.Id, Reference = x.ReturnNumber,
                Who = x.User.DisplayName, AtUtc = x.CreatedAtUtc, Amount = -x.TotalAmount
            }).ToListAsync(cancellationToken);
        var cashOuts = await _db.CashOuts.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).Take(take)
            .Select(x => new DashboardActivityDto
            {
                Kind = DashboardActivityKind.CashOut, Id = x.Id, Reference = x.Reason,
                Who = x.User.DisplayName, AtUtc = x.CreatedAtUtc, Amount = -x.Amount
            }).ToListAsync(cancellationToken);
        var purchases = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status == PurchaseInvoiceStatus.Confirmed && x.ConfirmedAtUtc != null)
            .OrderByDescending(x => x.ConfirmedAtUtc).Take(take)
            .Select(x => new DashboardActivityDto
            {
                Kind = DashboardActivityKind.Purchase, Id = x.Id, Reference = x.InvoiceNumber,
                Who = x.Supplier.NameEn, AtUtc = x.ConfirmedAtUtc!.Value, Amount = x.SubTotal
            }).ToListAsync(cancellationToken);

        return sales.Concat(returns).Concat(cashOuts).Concat(purchases)
            .OrderByDescending(x => x.AtUtc)
            .Take(take)
            .ToList();
    }
}
