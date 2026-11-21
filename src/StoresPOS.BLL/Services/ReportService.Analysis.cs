using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

/// <summary>Phase 23 reports: discounts/price changes, hours, product performance, profit, VAT, drawer, cheque aging.</summary>
public partial class ReportService
{
    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _time.LocalTimeZone);

    private DateTime TodayLocal => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).Date;

    private IQueryable<SalesInvoice> CompletedIn(ReportFilterBaseDto filter)
    {
        var q = _db.SalesInvoices.AsNoTracking().Where(x => x.Status == SalesInvoiceStatus.Completed);
        if (filter.FromUtc.HasValue) q = q.Where(x => x.CompletedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) q = q.Where(x => x.CompletedAtUtc < filter.ToUtc.Value);
        return q;
    }

    private IQueryable<SalesReturn> ReturnsIn(ReportFilterBaseDto filter)
    {
        var q = _db.SalesReturns.AsNoTracking().AsQueryable();
        if (filter.FromUtc.HasValue) q = q.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) q = q.Where(x => x.CreatedAtUtc < filter.ToUtc.Value);
        return q;
    }

    // ------------------------------------------------------------------ discounts and price changes

    public async Task<DiscountAuditReportDto> GetDiscountAuditAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var invoices = await CompletedIn(filter)
            .Select(x => new { x.Id, x.CashierUserId, Name = x.CashierUser.DisplayName, x.DiscountAmount, DiscountBy = x.DiscountByUserId })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(x => x.Id).ToList();

        var overrides = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(i => i.IsPriceOverridden && i.SalesInvoice.Status == SalesInvoiceStatus.Completed
                        && (!filter.FromUtc.HasValue || i.SalesInvoice.CompletedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || i.SalesInvoice.CompletedAtUtc < filter.ToUtc.Value))
            .Select(i => new
            {
                i.SalesInvoiceId,
                InvoiceNumber = i.SalesInvoice.InvoiceNumber!,
                AtUtc = i.SalesInvoice.CompletedAtUtc!.Value,
                i.ProductNameEn,
                i.ProductNameAr,
                i.Quantity,
                i.CatalogUnitPrice,
                i.UnitPrice,
                UserId = i.PriceOverriddenByUserId ?? i.SalesInvoice.CashierUserId,
                UserName = i.PriceOverriddenByUser != null ? i.PriceOverriddenByUser.DisplayName : i.SalesInvoice.CashierUser.DisplayName
            })
            .ToListAsync(cancellationToken);

        // Carts that were cancelled (held or open carts dropped, e.g. at shift close), by the cashier who started them.
        var cancelled = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Cancelled && x.Subtotal > 0
                        && (!filter.FromUtc.HasValue || x.UpdatedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || x.UpdatedAtUtc < filter.ToUtc.Value))
            .Select(x => new { x.CashierUserId, Name = x.CashierUser.DisplayName })
            .ToListAsync(cancellationToken);

        var returns = await ReturnsIn(filter)
            .Select(x => new { x.UserId, Name = x.User.DisplayName, x.TotalAmount })
            .ToListAsync(cancellationToken);

        var names = new Dictionary<int, string>();
        foreach (var i in invoices) names[i.CashierUserId] = i.Name;
        foreach (var o in overrides) names.TryAdd(o.UserId, o.UserName);
        foreach (var c in cancelled) names.TryAdd(c.CashierUserId, c.Name);
        foreach (var r in returns) names.TryAdd(r.UserId, r.Name);
        var discountByIds = invoices.Where(x => x.DiscountBy.HasValue).Select(x => x.DiscountBy!.Value).Where(id => !names.ContainsKey(id)).Distinct().ToList();
        if (discountByIds.Count > 0)
        {
            foreach (var u in await _db.Users.AsNoTracking().Where(x => discountByIds.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName }).ToListAsync(cancellationToken))
            {
                names[u.Id] = u.DisplayName;
            }
        }

        var users = names.Keys.Select(id => new DiscountAuditUserDto
        {
            UserId = id,
            Name = names[id],
            Invoices = invoices.Count(x => x.CashierUserId == id),
            DiscountCount = invoices.Count(x => x.DiscountAmount > 0 && (x.DiscountBy ?? x.CashierUserId) == id),
            DiscountAmount = invoices.Where(x => x.DiscountAmount > 0 && (x.DiscountBy ?? x.CashierUserId) == id).Sum(x => x.DiscountAmount),
            OverrideLines = overrides.Count(x => x.UserId == id),
            OverrideAmount = PricingCalculator.RoundMoney(overrides.Where(x => x.UserId == id).Sum(x => (x.CatalogUnitPrice - x.UnitPrice) * x.Quantity)),
            CancelledCount = cancelled.Count(x => x.CashierUserId == id),
            ReturnsCount = returns.Count(x => x.UserId == id),
            ReturnsAmount = returns.Where(x => x.UserId == id).Sum(x => x.TotalAmount)
        })
        .Where(x => x.Invoices + x.DiscountCount + x.OverrideLines + x.CancelledCount + x.ReturnsCount > 0)
        .OrderByDescending(x => x.DiscountAmount + x.OverrideAmount + x.ReturnsAmount)
        .ThenBy(x => x.Name)
        .ToList();

        return new DiscountAuditReportDto
        {
            Users = users,
            Overrides = overrides
                .OrderByDescending(x => x.AtUtc)
                .Take(500)
                .Select(x => new PriceOverrideLineDto
                {
                    InvoiceId = x.SalesInvoiceId,
                    InvoiceNumber = x.InvoiceNumber,
                    AtUtc = x.AtUtc,
                    ProductNameEn = x.ProductNameEn,
                    ProductNameAr = x.ProductNameAr,
                    Quantity = x.Quantity,
                    CatalogPrice = x.CatalogUnitPrice,
                    ChargedPrice = x.UnitPrice,
                    Difference = PricingCalculator.RoundMoney((x.CatalogUnitPrice - x.UnitPrice) * x.Quantity),
                    ByName = x.UserName
                })
                .ToList()
        };
    }

    // ------------------------------------------------------------------ sales by hour and weekday

    /// <summary>Saturday first (Egyptian week).</summary>
    public static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public async Task<SalesTimeReportDto> GetSalesTimeAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var rows = (await CompletedIn(filter)
                .Select(x => new { At = x.CompletedAtUtc!.Value, x.Total })
                .ToListAsync(cancellationToken))
            .Select(x => new { Local = ToLocal(x.At), x.Total })
            .ToList();

        var heat = WeekOrder.Select(_ => new decimal[24]).ToArray();
        foreach (var r in rows)
        {
            heat[Array.IndexOf(WeekOrder, r.Local.DayOfWeek)][r.Local.Hour] += r.Total;
        }

        return new SalesTimeReportDto
        {
            ByHour = Enumerable.Range(0, 24).Select(h => new NamedAmountDto
            {
                Id = h,
                Name = h.ToString("00"),
                Count = rows.Count(x => x.Local.Hour == h),
                Amount = rows.Where(x => x.Local.Hour == h).Sum(x => x.Total)
            }).ToList(),
            ByWeekday = WeekOrder.Select(d => new NamedAmountDto
            {
                Id = (int)d,
                Name = d.ToString(),
                Count = rows.Count(x => x.Local.DayOfWeek == d),
                Amount = rows.Where(x => x.Local.DayOfWeek == d).Sum(x => x.Total)
            }).ToList(),
            Heat = heat
        };
    }

    // ------------------------------------------------------------------ product performance

    public async Task<ProductPerformanceReportDto> GetProductPerformanceAsync(
        CashOutsReportFilterDto filter,
        int deadDays = 90,
        int top = 20,
        CancellationToken cancellationToken = default)
    {
        var sold = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(i => i.SalesInvoice.Status == SalesInvoiceStatus.Completed
                        && (!filter.FromUtc.HasValue || i.SalesInvoice.CompletedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || i.SalesInvoice.CompletedAtUtc < filter.ToUtc.Value))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Sales = g.Sum(x => x.LineTotal) })
            .ToListAsync(cancellationToken);

        var lastSold = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(i => i.SalesInvoice.Status == SalesInvoiceStatus.Completed)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Last = g.Max(x => x.SalesInvoice.CompletedAtUtc) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Last, cancellationToken);

        var products = await _db.Products.AsNoTracking()
            .Select(x => new { x.Id, x.NameEn, x.NameAr, x.Unit, x.StockQuantity, x.IsActive, Cost = x.AverageCost > 0 ? x.AverageCost : x.PurchasePrice, x.CreatedAtUtc })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        ProductPerformanceRowDto Row(int id, decimal qty, decimal sales)
        {
            var p = products[id];
            var last = lastSold.GetValueOrDefault(id);
            return new ProductPerformanceRowDto
            {
                ProductId = id,
                NameEn = p.NameEn,
                NameAr = p.NameAr,
                Unit = p.Unit,
                Quantity = qty,
                Sales = sales,
                Stock = p.StockQuantity,
                StockCostValue = PricingCalculator.RoundMoney(Math.Max(0, p.StockQuantity) * p.Cost),
                LastSoldUtc = last,
                DaysSinceSale = last.HasValue ? (int)(now - last.Value).TotalDays : null
            };
        }

        var soldRows = sold.Where(x => products.ContainsKey(x.ProductId)).Select(x => Row(x.ProductId, x.Qty, x.Sales)).ToList();
        var cutoff = now.AddDays(-deadDays);
        var dead = products.Values
            .Where(p => p.IsActive && p.StockQuantity > 0
                        && (lastSold.GetValueOrDefault(p.Id) is not { } last ? p.CreatedAtUtc < cutoff : last < cutoff))
            .Select(p => Row(p.Id, 0, 0))
            .OrderByDescending(x => x.StockCostValue)
            .ToList();

        return new ProductPerformanceReportDto
        {
            Best = soldRows.OrderByDescending(x => x.Sales).Take(top).ToList(),
            Worst = soldRows.OrderBy(x => x.Sales).Take(top).ToList(),
            Dead = dead,
            DeadDays = deadDays,
            ProductsSold = soldRows.Count
        };
    }

    // ------------------------------------------------------------------ profit

    /// <summary>
    /// Profit = net sales (invoice discount shared over lines) − cost (the weighted-average cost saved on each line),
    /// less returns made in the range (refund and the cost of the returned items).
    /// </summary>
    public async Task<ProfitReportDto> GetProfitAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lines = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(i => i.SalesInvoice.Status == SalesInvoiceStatus.Completed
                        && (!filter.FromUtc.HasValue || i.SalesInvoice.CompletedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || i.SalesInvoice.CompletedAtUtc < filter.ToUtc.Value))
            .Select(i => new
            {
                i.ProductId,
                i.ProductNameEn,
                i.ProductNameAr,
                i.Quantity,
                i.LineTotal,
                i.UnitCost,
                i.SalesInvoice.Subtotal,
                i.SalesInvoice.Total,
                At = i.SalesInvoice.CompletedAtUtc!.Value,
                CategoryId = i.Product.CategoryId,
                CatEn = i.Product.Category != null ? i.Product.Category.NameEn : null,
                CatAr = i.Product.Category != null ? i.Product.Category.NameAr : null
            })
            .ToListAsync(cancellationToken);

        var returns = await _db.SalesReturnItems.AsNoTracking()
            .Where(r => (!filter.FromUtc.HasValue || r.SalesReturn.CreatedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || r.SalesReturn.CreatedAtUtc < filter.ToUtc.Value))
            .Select(r => new
            {
                r.ProductId,
                r.ProductNameEn,
                r.ProductNameAr,
                r.Quantity,
                r.RefundAmount,
                Cost = r.SalesInvoiceItem.UnitCost * r.Quantity,
                At = r.SalesReturn.CreatedAtUtc,
                CategoryId = r.Product.CategoryId,
                CatEn = r.Product.Category != null ? r.Product.Category.NameEn : null,
                CatAr = r.Product.Category != null ? r.Product.Category.NameAr : null
            })
            .ToListAsync(cancellationToken);

        var entries = lines.Select(x => new
            {
                x.ProductId, x.ProductNameEn, x.ProductNameAr, Qty = x.Quantity,
                Sales = x.Subtotal == 0 ? 0m : PricingCalculator.RoundMoney(x.LineTotal * x.Total / x.Subtotal),
                Cost = PricingCalculator.RoundMoney(x.UnitCost * x.Quantity),
                Day = ToLocal(x.At).Date, x.CategoryId, x.CatEn, x.CatAr
            })
            .Concat(returns.Select(x => new
            {
                x.ProductId, x.ProductNameEn, x.ProductNameAr, Qty = -x.Quantity,
                Sales = -x.RefundAmount,
                Cost = -PricingCalculator.RoundMoney(x.Cost),
                Day = ToLocal(x.At).Date, x.CategoryId, x.CatEn, x.CatAr
            }))
            .ToList();

        static ProfitRowDto Sum<T>(int? id, string en, string ar, IEnumerable<T> g, Func<T, decimal> qty, Func<T, decimal> sales, Func<T, decimal> cost) => new()
        {
            Id = id, NameEn = en, NameAr = ar,
            Quantity = g.Sum(qty), Sales = g.Sum(sales), Cost = g.Sum(cost)
        };

        return new ProfitReportDto
        {
            Sales = entries.Sum(x => x.Sales),
            Cost = entries.Sum(x => x.Cost),
            ByProduct = entries.GroupBy(x => x.ProductId)
                .Select(g => Sum(g.Key, g.First().ProductNameEn, g.First().ProductNameAr, g, x => x.Qty, x => x.Sales, x => x.Cost))
                .OrderByDescending(x => x.Profit).ToList(),
            ByCategory = entries.GroupBy(x => x.CategoryId)
                .Select(g => Sum(g.Key, g.First().CatEn ?? "—", g.First().CatAr ?? "—", g, x => x.Qty, x => x.Sales, x => x.Cost))
                .OrderByDescending(x => x.Profit).ToList(),
            ByDay = entries.GroupBy(x => x.Day).OrderBy(g => g.Key)
                .Select(g => Sum(null, g.Key.ToString("yyyy-MM-dd"), g.Key.ToString("yyyy-MM-dd"), g, x => x.Qty, x => x.Sales, x => x.Cost))
                .ToList()
        };
    }

    // ------------------------------------------------------------------ VAT

    /// <summary>
    /// VAT in sales and returns per store-local day at the given rate. Prices including VAT: VAT = total − total ÷ (1 + rate);
    /// otherwise VAT is added on top: total × rate. The rate is today's setting (old invoices do not store their rate).
    /// </summary>
    public async Task<TaxReportDto> GetTaxAsync(CashOutsReportFilterDto filter, decimal vatPercent, bool pricesIncludeVat, CancellationToken cancellationToken = default)
    {
        decimal Vat(decimal amount) => vatPercent <= 0 ? 0m
            : pricesIncludeVat
                ? PricingCalculator.RoundMoney(amount - amount / (1 + vatPercent / 100m))
                : PricingCalculator.RoundMoney(amount * vatPercent / 100m);

        var sales = (await CompletedIn(filter).Select(x => new { At = x.CompletedAtUtc!.Value, x.Total }).ToListAsync(cancellationToken))
            .Select(x => new { Day = ToLocal(x.At).Date, x.Total, Vat = Vat(x.Total) }).ToList();
        var returns = (await ReturnsIn(filter).Select(x => new { At = x.CreatedAtUtc, x.TotalAmount }).ToListAsync(cancellationToken))
            .Select(x => new { Day = ToLocal(x.At).Date, x.TotalAmount, Vat = Vat(x.TotalAmount) }).ToList();

        var days = sales.Select(x => x.Day).Concat(returns.Select(x => x.Day)).Distinct().OrderBy(x => x);
        return new TaxReportDto
        {
            VatPercent = vatPercent,
            PricesIncludeVat = pricesIncludeVat,
            Days = days.Select(d => new TaxDayDto
            {
                Date = d,
                Sales = sales.Where(x => x.Day == d).Sum(x => x.Total),
                SalesVat = sales.Where(x => x.Day == d).Sum(x => x.Vat),
                Returns = returns.Where(x => x.Day == d).Sum(x => x.TotalAmount),
                ReturnsVat = returns.Where(x => x.Day == d).Sum(x => x.Vat)
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ drawer differences

    public async Task<DrawerReportDto> GetDrawerAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var shifts = await _db.Shifts.AsNoTracking()
            .Where(x => x.Status == ShiftStatus.Closed && x.CashDifference != null
                        && (!filter.FromUtc.HasValue || x.ClosedAtUtc >= filter.FromUtc.Value)
                        && (!filter.ToUtc.HasValue || x.ClosedAtUtc < filter.ToUtc.Value))
            .OrderBy(x => x.ClosedAtUtc)
            .Select(x => new
            {
                x.Id,
                ClosedAt = x.ClosedAtUtc!.Value,
                ClosedBy = x.ClosedByUser != null ? x.ClosedByUser.DisplayName : "",
                Difference = x.CashDifference!.Value
            })
            .ToListAsync(cancellationToken);
        var ids = shifts.Select(x => x.Id).ToList();
        var participants = await _db.ShiftParticipants.AsNoTracking()
            .Where(x => ids.Contains(x.ShiftId))
            .Select(x => new { x.ShiftId, x.UserId, x.User.DisplayName })
            .ToListAsync(cancellationToken);

        var users = participants
            .GroupBy(x => new { x.UserId, x.DisplayName })
            .Select(g =>
            {
                var diffs = g.Select(p => shifts.First(s => s.Id == p.ShiftId).Difference).ToList();
                return new DrawerUserDto
                {
                    UserId = g.Key.UserId,
                    Name = g.Key.DisplayName,
                    Shifts = diffs.Count,
                    ShortageCount = diffs.Count(d => d < 0),
                    ShortageTotal = diffs.Where(d => d < 0).Sum(),
                    SurplusCount = diffs.Count(d => d > 0),
                    SurplusTotal = diffs.Where(d => d > 0).Sum()
                };
            })
            .OrderByDescending(x => x.ShortageCount)
            .ThenBy(x => x.ShortageTotal)
            .ToList();

        return new DrawerReportDto
        {
            Users = users,
            Shifts = shifts.Select(s => new DrawerShiftDto
            {
                ShiftId = s.Id,
                ClosedAtUtc = s.ClosedAt,
                ClosedBy = s.ClosedBy,
                Participants = participants.Where(p => p.ShiftId == s.Id).Select(p => p.DisplayName).ToList(),
                Difference = s.Difference
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ cheque aging

    public static readonly string[] AgingBuckets = ["notdue", "1-30", "31-60", "61-90", "90+"];

    public static string AgingBucket(int daysOverdue) => daysOverdue switch
    {
        <= 0 => "notdue",
        <= 30 => "1-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        _ => "90+"
    };

    public async Task<ChequeAgingReportDto> GetChequeAgingAsync(CancellationToken cancellationToken = default)
    {
        var today = TodayLocal;
        var rows = (await _db.Cheques.AsNoTracking()
                .Where(x => x.Status == ChequeStatus.Pending)
                .OrderBy(x => x.DueDate)
                .Select(x => new
                {
                    x.Id, x.Number, x.Bank, x.Direction, x.DueDate, x.Amount,
                    Party = x.Customer != null ? x.Customer.Name : x.Supplier != null ? x.Supplier.NameEn : null
                })
                .ToListAsync(cancellationToken))
            .Select(x =>
            {
                var days = (today - x.DueDate.Date).Days;
                return new ChequeAgingRowDto
                {
                    Id = x.Id,
                    Number = x.Number,
                    Bank = x.Bank,
                    Incoming = x.Direction == ChequeDirection.In,
                    PartyName = x.Party,
                    DueDate = x.DueDate,
                    Amount = x.Amount,
                    DaysOverdue = days,
                    Bucket = AgingBucket(days)
                };
            })
            .OrderByDescending(x => x.DaysOverdue)
            .ToList();

        return new ChequeAgingReportDto
        {
            AsOfLocal = today,
            Rows = rows,
            Buckets = AgingBuckets.Select(b => new ChequeAgingBucketDto
            {
                Key = b,
                InCount = rows.Count(x => x.Bucket == b && x.Incoming),
                InAmount = rows.Where(x => x.Bucket == b && x.Incoming).Sum(x => x.Amount),
                OutCount = rows.Count(x => x.Bucket == b && !x.Incoming),
                OutAmount = rows.Where(x => x.Bucket == b && !x.Incoming).Sum(x => x.Amount)
            }).ToList()
        };
    }
}
