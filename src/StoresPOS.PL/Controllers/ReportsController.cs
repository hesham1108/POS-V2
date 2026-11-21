using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Reports;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Reports (docs/page-composition.md §20, Phase 23): one shell — period presets + filters, summary cards,
/// one chart, tables, Excel / CSV / A4 print. Every export is built from the same DTO as the screen.
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
    private readonly IOptionsMonitor<StoreSettings> _store;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly TimeProvider _time;

    public ReportsController(
        IReportService reports,
        IChequeService cheques,
        ICategoryService categories,
        ISupplierService suppliers,
        IWarehouseService warehouses,
        IOptionsMonitor<StoreSettings> store,
        IStringLocalizer<SharedResource> localizer,
        TimeProvider time)
    {
        _reports = reports;
        _cheques = cheques;
        _categories = categories;
        _suppliers = suppliers;
        _warehouses = warehouses;
        _store = store;
        _l = localizer;
        _time = time;
    }

    private static bool IsAr => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
    private bool CanSeeProfit => User.HasPermission(AppPermissions.ProfitView);
    private string T(string key) => _l[key].Value;
    private static string Pick(string en, string? ar) => IsAr && !string.IsNullOrEmpty(ar) ? ar : en;

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Sales));

    // ================================================================== existing reports

    [HttpGet]
    public async Task<IActionResult> Sales(
        DateTime? from,
        DateTime? to,
        int? cashierId,
        PaymentMethod? method,
        SaleType? saleType,
        int? categoryId,
        int? customerGroup,
        CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var group = customerGroup is >= 0 and <= 3 ? customerGroup : null;
        SalesReportFilterDto Filter(DateTime a, DateTime b) => new()
        {
            FromUtc = ToUtc(a),
            ToUtc = ToUtc(b.AddDays(1)),
            CashierUserId = cashierId,
            PaymentMethod = method,
            SaleType = saleType,
            CategoryId = categoryId,
            CustomerGroup = group
        };

        var model = new SalesReportViewModel
        {
            From = f,
            To = t,
            CashierId = cashierId,
            Method = method,
            SaleType = saleType,
            CategoryId = categoryId,
            CustomerGroup = group,
            CanSeeProfit = CanSeeProfit,
            Users = await UsersAsync(cancellationToken),
            Categories = await CategoriesAsync(cancellationToken),
            Report = await _reports.GetSalesAsync(Filter(f, t), cancellationToken)
        };
        model.Previous = await _reports.GetSalesAsync(Filter(model.PreviousFrom, model.PreviousTo), cancellationToken);
        var r = model.Report;

        // Chart: this period by day vs the same day of the previous period.
        var days = Enumerable.Range(0, (t - f).Days + 1).Select(i => f.AddDays(i)).ToList();
        model.Chart = new ReportChart
        {
            Title = T("Net sales by day"),
            Type = "bar",
            TimeAxis = true,
            Labels = days.Select(d => d.ToString("MM/dd")).ToList(),
            Series =
            [
                new(T("Previous period"), days.Select(d => model.Previous.ByDay.FirstOrDefault(x => x.Date == d.AddDays(-days.Count))?.Amount ?? 0m).ToList(), Muted: true),
                new(T("This period"), days.Select(d => r.ByDay.FirstOrDefault(x => x.Date == d)?.Amount ?? 0m).ToList())
            ]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("sales-report", T("Sales report"), model);
            x.Summary.Add((T("Invoices"), r.InvoiceCount));
            x.Summary.Add((T("Before discounts"), r.GrossSales));
            x.Summary.Add((T("Discounts"), r.Discounts));
            x.Summary.Add((T("Net sales"), r.NetSales));
            x.Summary.Add((T("Returns"), r.ReturnsAmount));
            x.Summary.Add((T("After returns"), r.NetAfterReturns));
            x.Summary.Add((T("Previous period"), model.Previous.NetSales));
            if (model.CanSeeProfit)
            {
                x.Summary.Add((T("Cost"), r.Cost));
                x.Summary.Add((T("Gross profit"), r.Profit));
            }

            var byDay = x.AddSheet(T("By day"), T("Date"), T("Invoices"), T("Amount"));
            foreach (var d in r.ByDay) byDay.Add(d.Date, d.Count, d.Amount);
            byDay.Total = [T("Total"), r.ByDay.Sum(d => d.Count), r.ByDay.Sum(d => d.Amount)];
            var byMethod = x.AddSheet(T("By payment method"), T("Method"), T("Invoices"), T("Amount"));
            foreach (var m in r.ByMethod) byMethod.Add(Pick(m.NameEn, m.NameAr), m.Count, m.Amount);
            var byCashier = x.AddSheet(T("By cashier"), T("Cashier"), T("Invoices"), T("Amount"));
            foreach (var c in r.ByCashier) byCashier.Add(c.Name, c.Count, c.Amount);
            var top = model.CanSeeProfit
                ? x.AddSheet(T("Top products"), T("Product"), T("Qty"), T("Amount"), T("Profit"))
                : x.AddSheet(T("Top products"), T("Product"), T("Qty"), T("Amount"));
            foreach (var p in r.TopProducts)
            {
                if (model.CanSeeProfit) top.Add(Pick(p.NameEn, p.NameAr), p.Quantity, p.Amount, p.Profit);
                else top.Add(Pick(p.NameEn, p.NameAr), p.Quantity, p.Amount);
            }

            var customers = x.AddSheet(T("Top customers"), T("Customer"), T("Invoices"), T("Amount"));
            foreach (var c in r.TopCustomers) customers.Add(c.Name, c.Count, c.Amount);
            return x.ToFile(format);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Purchases(DateTime? from, DateTime? to, int? supplierId, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var suppliers = await _suppliers.GetListAsync(new SupplierListFilterDto { IsActive = null }, cancellationToken);
        var model = new PurchasesReportViewModel
        {
            From = f,
            To = t,
            SupplierId = supplierId,
            Suppliers = suppliers.Select(x => new ReportOption { Id = x.Id, Name = Pick(x.NameEn, x.NameAr) }).ToList(),
            // Purchase invoice dates are calendar dates (no time zone), so the range is used as-is.
            Report = await _reports.GetPurchasesAsync(new PurchasesReportFilterDto { FromUtc = f, ToUtc = t.AddDays(1), SupplierId = supplierId }, cancellationToken)
        };
        var r = model.Report;
        var top = r.BySupplier.Take(10).ToList();
        model.Chart = new ReportChart
        {
            Title = T("Purchases by supplier"),
            Type = "bar",
            Horizontal = true,
            Labels = top.Select(s => s.Name).ToList(),
            Series = [new(T("Amount"), top.Select(s => s.Amount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("purchases-report", T("Purchases report"), model);
            x.Summary.Add((T("Invoices"), r.InvoiceCount));
            x.Summary.Add((T("Total"), r.Total));
            var inv = x.AddSheet(T("Invoices"), T("Invoice"), T("Date"), T("Supplier"), T("Items"), T("Total"));
            foreach (var i in r.Invoices) inv.Add(i.InvoiceNumber, i.InvoiceDate.Date, i.SupplierName, i.LineCount, i.Total);
            inv.Total = [T("Total"), null, null, r.Invoices.Sum(i => i.LineCount), r.Total];
            var bySupplier = x.AddSheet(T("By supplier"), T("Supplier"), T("Invoices"), T("Amount"));
            foreach (var s in r.BySupplier) bySupplier.Add(s.Name, s.Count, s.Amount);
            var byProduct = x.AddSheet(T("By product"), T("Product"), T("Qty"), T("Amount"));
            foreach (var p in r.ByProduct) byProduct.Add(Pick(p.Name, p.NameAr), p.Quantity, p.Amount);
            return x.ToFile(format);
        }

        return View(model);
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
        var model = new InventoryReportViewModel
        {
            From = f,
            To = t,
            CategoryId = categoryId,
            WarehouseId = warehouseId,
            LowOnly = lowOnly,
            Categories = await CategoriesAsync(cancellationToken),
            Warehouses = warehouses.Select(w => new ReportOption { Id = w.Id, Name = Ui.Name(w.NameEn, w.NameAr) }).ToList(),
            Report = await _reports.GetInventoryAsync(new InventoryReportFilterDto
            {
                FromUtc = ToUtc(f),
                ToUtc = ToUtc(t.AddDays(1)),
                CategoryId = categoryId,
                WarehouseId = warehouseId,
                LowStockOnly = lowOnly
            }, cancellationToken)
        };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Stock movements in the period"),
            Type = "bar",
            Labels = r.Movements.Select(m => T(m.Type.ToString())).ToList(),
            Series = [new(T("In"), r.Movements.Select(m => m.QuantityIn).ToList()), new(T("Out"), r.Movements.Select(m => m.QuantityOut).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("inventory-report", T("Inventory report"), model);
            x.Summary.Add((T("Products"), r.ProductCount));
            x.Summary.Add((T("Low stock"), r.LowStockCount));
            x.Summary.Add((T("Out of stock"), r.OutOfStockCount));
            x.Summary.Add((T("Stock value at cost"), r.TotalCostValue));
            x.Summary.Add((T("Stock value at retail"), r.TotalRetailValue));
            var rows = x.AddSheet(T("Stock"), T("Product"), T("Category"), T("Unit"), T("Stock"), T("Low-stock level"), T("Cost"), T("Retail price"), T("Value at cost"), T("Value at retail"));
            foreach (var p in r.Rows)
            {
                rows.Add(Pick(p.NameEn, p.NameAr), Pick(p.CategoryNameEn ?? "", p.CategoryNameAr), p.Unit, p.StockQuantity, p.LowStockThreshold, p.UnitCost, p.RetailPrice, p.CostValue, p.RetailValue);
            }

            rows.Total = [T("Total"), null, null, null, null, null, null, r.TotalCostValue, r.TotalRetailValue];
            var moves = x.AddSheet(T("Movements"), T("Type"), T("Count"), T("In"), T("Out"));
            foreach (var m in r.Movements) moves.Add(T(m.Type.ToString()), m.Count, m.QuantityIn, m.QuantityOut);
            return x.ToFile(format);
        }

        return View(model);
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
        var model = new ShiftsReportViewModel
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
        };
        var r = model.Report;
        var closed = r.Shifts.Where(s => s.CashDifference.HasValue).OrderBy(s => s.OpenedAtUtc).ToList();
        model.Chart = new ReportChart
        {
            Title = T("Drawer difference per shift"),
            Type = "bar",
            TimeAxis = true,
            Labels = closed.Select(s => "#" + s.Id).ToList(),
            Series = [new(T("Difference"), closed.Select(s => s.CashDifference!.Value).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("shifts-report", T("Shifts report"), model);
            x.Summary.Add((T("Shifts"), r.ShiftCount));
            x.Summary.Add((T("Total sales"), r.SalesTotal));
            x.Summary.Add((T("Surplus"), r.SurplusTotal));
            x.Summary.Add((T("Shortage"), r.ShortageTotal));
            x.Summary.Add((T("Net difference"), r.NetDifference));
            var rows = x.AddSheet(T("Shifts"), T("Shift"), T("Opened"), T("Closed"), T("Opened by"), T("Closed by"), T("Participants"), T("Invoices"), T("Sales"), T("Expected in drawer"), T("Actual cash"), T("Difference"), T("Result"));
            foreach (var s in r.Shifts)
            {
                rows.Add(s.Id, s.OpenedAtUtc.ToLocalTime(), s.ClosedAtUtc?.ToLocalTime(), s.OpenedByDisplayName, s.ClosedByDisplayName,
                    string.Join(", ", s.Participants), s.InvoiceCount, s.SalesTotal, s.ExpectedCash, s.ActualCash, s.CashDifference,
                    s.Reconciliation is { } rec ? T(rec == ShiftReconciliation.NotReconciled ? "Not reconciled" : rec.ToString()) : T("Shift open"));
            }

            var byCashier = x.AddSheet(T("By cashier"), T("Cashier"), T("Invoices"), T("Amount"));
            foreach (var c in r.ByCashier) byCashier.Add(c.Name, c.Count, c.Amount);
            return x.ToFile(format);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CashOuts(DateTime? from, DateTime? to, int? userId, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new CashOutsReportViewModel
        {
            From = f,
            To = t,
            UserId = userId,
            Users = await UsersAsync(cancellationToken),
            Report = await _reports.GetCashOutsAsync(new CashOutsReportFilterDto { FromUtc = ToUtc(f), ToUtc = ToUtc(t.AddDays(1)), UserId = userId }, cancellationToken)
        };
        var r = model.Report;
        var reasons = r.ByReason.Take(8).ToList();
        model.Chart = new ReportChart
        {
            Title = T("Cash-outs by reason"),
            Type = "doughnut",
            Labels = reasons.Select(x => x.Name).ToList(),
            Series = [new(T("Amount"), reasons.Select(x => x.Amount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("cash-outs-report", T("Cash-outs report"), model);
            x.Summary.Add((T("Cash-outs"), r.Items.Count));
            x.Summary.Add((T("Total"), r.Total));
            var items = x.AddSheet(T("Cash-outs"), T("Date"), T("Shift"), T("User"), T("Reason"), T("Amount"));
            foreach (var i in r.Items) items.Add(i.CreatedAtUtc.ToLocalTime(), i.ShiftId, i.UserDisplayName, i.Reason, i.Amount);
            items.Total = [T("Total"), null, null, null, r.Total];
            var byUser = x.AddSheet(T("By user"), T("User"), T("Count"), T("Amount"));
            foreach (var u in r.ByUser) byUser.Add(u.Name, u.Count, u.Amount);
            var byReason = x.AddSheet(T("By reason"), T("Reason"), T("Count"), T("Amount"));
            foreach (var u in r.ByReason) byReason.Add(u.Name, u.Count, u.Amount);
            return x.ToFile(format);
        }

        return View(model);
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
        var model = new ChequesReportViewModel
        {
            From = f,
            To = t,
            Direction = direction,
            Status = status,
            Report = await _cheques.GetReportAsync(new ChequesReportFilterDto { DueFrom = f, DueTo = t, Direction = direction, Status = status }, cancellationToken)
        };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Cheques by status"),
            Type = "doughnut",
            Labels = r.ByStatus.Select(s => Pick(s.Name, s.NameAr)).ToList(),
            Series = [new(T("Amount"), r.ByStatus.Select(s => s.Amount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("cheques-report", T("Cheques report"), model);
            x.Summary.Add((T("Cheques"), r.Rows.Count));
            x.Summary.Add((T("Total"), r.TotalAmount));
            var rows = x.AddSheet(T("Cheques"), T("Number"), T("Direction"), T("Bank"), T("Due date"), T("Amount"), T("Status"), T("Customer / supplier"), T("Invoice"));
            foreach (var c in r.Rows)
            {
                rows.Add(c.Number, c.Direction == ChequeDirection.In ? T("Receivable") : T("Payable"), c.Bank, c.DueDate.Date, c.Amount,
                    T(c.Status.ToString()), c.CustomerName ?? c.SupplierName, c.SalesInvoiceNumber ?? c.PurchaseInvoiceNumber);
            }

            rows.Total = [T("Total"), null, null, null, r.TotalAmount, null, null, null];
            var byStatus = x.AddSheet(T("By status"), T("Status"), T("Count"), T("Amount"));
            foreach (var s in r.ByStatus) byStatus.Add(Pick(s.Name, s.NameAr), s.Count, s.Amount);
            return x.ToFile(format);
        }

        return View(model);
    }

    // ================================================================== new reports (Phase 23)

    /// <summary>Discounts, price changes, cancelled carts and returns per person.</summary>
    [HttpGet]
    public async Task<IActionResult> Audit(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new AnalysisReportViewModel<DiscountAuditReportDto> { From = f, To = t, Report = await _reports.GetDiscountAuditAsync(DateFilter(f, t), cancellationToken) };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Discounts and price changes per person"),
            Type = "bar",
            Horizontal = true,
            Labels = r.Users.Select(u => u.Name).ToList(),
            Series = [new(T("Discounts"), r.Users.Select(u => u.DiscountAmount).ToList()), new(T("Price changes"), r.Users.Select(u => u.OverrideAmount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("discounts-report", T("Discounts and price changes"), model);
            x.Summary.Add((T("Discounts"), r.DiscountTotal));
            x.Summary.Add((T("Price changes"), r.OverrideTotal));
            x.Summary.Add((T("Cancelled carts"), r.CancelledTotal));
            x.Summary.Add((T("Returns"), r.ReturnsTotal));
            var users = x.AddSheet(T("By person"), T("User"), T("Invoices"), T("Discounts"), T("Discount amount"), T("Price changes"), T("Price change amount"), T("Cancelled carts"), T("Returns"), T("Returns amount"));
            foreach (var u in r.Users) users.Add(u.Name, u.Invoices, u.DiscountCount, u.DiscountAmount, u.OverrideLines, u.OverrideAmount, u.CancelledCount, u.ReturnsCount, u.ReturnsAmount);
            var lines = x.AddSheet(T("Price changes"), T("Invoice"), T("Date"), T("Product"), T("Qty"), T("Catalog price"), T("Charged price"), T("Difference"), T("By"));
            foreach (var o in r.Overrides) lines.Add(o.InvoiceNumber, o.AtUtc.ToLocalTime(), Pick(o.ProductNameEn, o.ProductNameAr), o.Quantity, o.CatalogPrice, o.ChargedPrice, o.Difference, o.ByName);
            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>When the store is busy: sales by hour and by weekday.</summary>
    [HttpGet]
    public async Task<IActionResult> Hours(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new AnalysisReportViewModel<SalesTimeReportDto> { From = f, To = t, Report = await _reports.GetSalesTimeAsync(DateFilter(f, t), cancellationToken) };
        var r = model.Report;
        var hours = r.ByHour.Where(h => h.Count > 0).Select(h => h.Id!.Value).DefaultIfEmpty(9).ToList();
        var first = Math.Min(hours.Min(), 9);
        var last = Math.Max(hours.Max(), 21);
        model.Chart = new ReportChart
        {
            Title = T("Sales by hour"),
            Type = "bar",
            TimeAxis = true,
            Labels = r.ByHour.Where(h => h.Id >= first && h.Id <= last).Select(h => h.Name).ToList(),
            Series = [new(T("Amount"), r.ByHour.Where(h => h.Id >= first && h.Id <= last).Select(h => h.Amount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("sales-by-hour", T("Sales by hour and day"), model);
            x.Summary.Add((T("Invoices"), r.InvoiceCount));
            x.Summary.Add((T("Total"), r.Total));
            var byHour = x.AddSheet(T("By hour"), T("Hour"), T("Invoices"), T("Amount"));
            foreach (var h in r.ByHour) byHour.Add(h.Name, h.Count, h.Amount);
            var byDay = x.AddSheet(T("By weekday"), T("Day"), T("Invoices"), T("Amount"));
            foreach (var d in r.ByWeekday) byDay.Add(WeekdayName(d.Id!.Value), d.Count, d.Amount);
            var heat = x.AddSheet(T("Hour by day"), new[] { T("Day") }.Concat(Enumerable.Range(0, 24).Select(h => h.ToString("00"))).ToArray());
            for (var i = 0; i < r.Heat.Length; i++)
            {
                heat.Add(new object?[] { WeekdayName((int)ReportService.WeekOrder[i]) }.Concat(r.Heat[i].Cast<object?>()).ToArray());
            }

            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>Best and worst sellers in the period, and stock that has not sold for 90 days.</summary>
    [HttpGet]
    public async Task<IActionResult> Products(DateTime? from, DateTime? to, int days = 90, CancellationToken cancellationToken = default)
    {
        var (f, t) = Range(from, to);
        days = Math.Clamp(days, 7, 365);
        var model = new AnalysisReportViewModel<ProductPerformanceReportDto>
        {
            From = f,
            To = t,
            Report = await _reports.GetProductPerformanceAsync(DateFilter(f, t), days, 20, cancellationToken)
        };
        var r = model.Report;
        var best = r.Best.Take(10).ToList();
        model.Chart = new ReportChart
        {
            Title = T("Best sellers"),
            Type = "bar",
            Horizontal = true,
            Labels = best.Select(p => Pick(p.NameEn, p.NameAr)).ToList(),
            Series = [new(T("Sales"), best.Select(p => p.Sales).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("product-performance", T("Product performance"), model);
            x.Summary.Add((T("Products sold"), r.ProductsSold));
            x.Summary.Add((string.Format(T("Not sold for {0} days"), r.DeadDays), r.Dead.Count));
            x.Summary.Add((T("Value of slow stock (cost)"), r.DeadStockValue));
            void Sheet(string name, IEnumerable<ProductPerformanceRowDto> rows)
            {
                var s = x.AddSheet(name, T("Product"), T("Qty"), T("Sales"), T("Stock"), T("Value at cost"), T("Last sold"), T("Days since sale"));
                foreach (var p in rows) s.Add(Pick(p.NameEn, p.NameAr), p.Quantity, p.Sales, p.Stock, p.StockCostValue, p.LastSoldUtc?.ToLocalTime(), p.DaysSinceSale);
            }

            Sheet(T("Best sellers"), r.Best);
            Sheet(T("Worst sellers"), r.Worst);
            Sheet(T("Slow stock"), r.Dead);
            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>Profit by product, category and day — Owner/Admin (Profit.View).</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.ProfitView)]
    public async Task<IActionResult> Profit(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new AnalysisReportViewModel<ProfitReportDto> { From = f, To = t, Report = await _reports.GetProfitAsync(DateFilter(f, t), cancellationToken) };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Profit by day"),
            Type = "line",
            TimeAxis = true,
            Labels = r.ByDay.Select(d => d.NameEn[5..].Replace('-', '/')).ToList(),
            Series = [new(T("Sales"), r.ByDay.Select(d => d.Sales).ToList(), Muted: true), new(T("Profit"), r.ByDay.Select(d => d.Profit).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("profit-report", T("Profit report"), model);
            x.Summary.Add((T("Net sales"), r.Sales));
            x.Summary.Add((T("Cost"), r.Cost));
            x.Summary.Add((T("Gross profit"), r.Profit));
            x.Summary.Add((T("Margin %"), r.MarginPercent));
            void Sheet(string name, string first, IEnumerable<ProfitRowDto> rows)
            {
                var s = x.AddSheet(name, first, T("Qty"), T("Net sales"), T("Cost"), T("Gross profit"), T("Margin %"));
                foreach (var p in rows) s.Add(Pick(p.NameEn, p.NameAr), p.Quantity, p.Sales, p.Cost, p.Profit, p.MarginPercent);
            }

            Sheet(T("By product"), T("Product"), r.ByProduct);
            Sheet(T("By category"), T("Category"), r.ByCategory);
            Sheet(T("By day"), T("Date"), r.ByDay);
            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>VAT collected in sales and given back in returns.</summary>
    [HttpGet]
    public async Task<IActionResult> Tax(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var store = _store.CurrentValue;
        var model = new AnalysisReportViewModel<TaxReportDto>
        {
            From = f,
            To = t,
            Report = await _reports.GetTaxAsync(DateFilter(f, t), store.VatPercent, store.PricesIncludeVat, cancellationToken)
        };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Net VAT by day"),
            Type = "bar",
            TimeAxis = true,
            Labels = r.Days.Select(d => d.Date.ToString("MM/dd")).ToList(),
            Series = [new(T("Net VAT"), r.Days.Select(d => d.NetVat).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("vat-report", T("VAT report"), model);
            x.Summary.Add((T("VAT %"), r.VatPercent));
            x.Summary.Add((T("VAT in sales"), r.SalesVat));
            x.Summary.Add((T("VAT in returns"), r.ReturnsVat));
            x.Summary.Add((T("Net VAT"), r.NetVat));
            var s = x.AddSheet(T("By day"), T("Date"), T("Sales"), T("VAT in sales"), T("Returns"), T("VAT in returns"), T("Net VAT"));
            foreach (var d in r.Days) s.Add(d.Date, d.Sales, d.SalesVat, d.Returns, d.ReturnsVat, d.NetVat);
            s.Total = [T("Total"), r.Sales, r.SalesVat, r.Returns, r.ReturnsVat, r.NetVat];
            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>Drawer shortages and surpluses per person over time.</summary>
    [HttpGet]
    public async Task<IActionResult> Drawer(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (f, t) = Range(from, to);
        var model = new AnalysisReportViewModel<DrawerReportDto> { From = f, To = t, Report = await _reports.GetDrawerAsync(DateFilter(f, t), cancellationToken) };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Shortage and surplus per person"),
            Type = "bar",
            Horizontal = true,
            Labels = r.Users.Select(u => u.Name).ToList(),
            Series = [new(T("Shortage"), r.Users.Select(u => u.ShortageTotal).ToList()), new(T("Surplus"), r.Users.Select(u => u.SurplusTotal).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("drawer-differences", T("Drawer differences"), model);
            x.Summary.Add((T("Shortage"), r.ShortageTotal));
            x.Summary.Add((T("Surplus"), r.SurplusTotal));
            var users = x.AddSheet(T("By person"), T("User"), T("Shifts"), T("Short"), T("Shortage"), T("Over"), T("Surplus"), T("Net"), T("Short %"));
            foreach (var u in r.Users) users.Add(u.Name, u.Shifts, u.ShortageCount, u.ShortageTotal, u.SurplusCount, u.SurplusTotal, u.Net, u.ShortagePercent);
            var shifts = x.AddSheet(T("Shifts"), T("Shift"), T("Closed"), T("Closed by"), T("Participants"), T("Difference"));
            foreach (var s in r.Shifts) shifts.Add(s.ShiftId, s.ClosedAtUtc.ToLocalTime(), s.ClosedBy, string.Join(", ", s.Participants), s.Difference);
            return x.ToFile(format);
        }

        return View(model);
    }

    /// <summary>Pending cheques by how late they are (as of today).</summary>
    [HttpGet]
    public async Task<IActionResult> ChequeAging(CancellationToken cancellationToken)
    {
        var today = TodayLocal;
        var model = new AnalysisReportViewModel<ChequeAgingReportDto>
        {
            From = today,
            To = today,
            DateRange = false,
            Report = await _reports.GetChequeAgingAsync(cancellationToken)
        };
        var r = model.Report;
        model.Chart = new ReportChart
        {
            Title = T("Pending cheques by age"),
            Type = "bar",
            Labels = r.Buckets.Select(b => AgingLabel(b.Key)).ToList(),
            Series = [new(T("Receivable"), r.Buckets.Select(b => b.InAmount).ToList()), new(T("Payable"), r.Buckets.Select(b => b.OutAmount).ToList())]
        };

        if (ReportExport.Wants(Request, out var format))
        {
            var x = NewExport("cheque-aging", T("Cheque aging"), model);
            x.Summary.Add((T("Overdue receivable"), r.OverdueIn));
            x.Summary.Add((T("Overdue payable"), r.OverdueOut));
            var buckets = x.AddSheet(T("Ages"), T("Age"), T("Receivable count"), T("Receivable"), T("Payable count"), T("Payable"));
            foreach (var b in r.Buckets) buckets.Add(AgingLabel(b.Key), b.InCount, b.InAmount, b.OutCount, b.OutAmount);
            var rows = x.AddSheet(T("Cheques"), T("Number"), T("Direction"), T("Bank"), T("Customer / supplier"), T("Due date"), T("Days late"), T("Age"), T("Amount"));
            foreach (var c in r.Rows) rows.Add(c.Number, c.Incoming ? T("Receivable") : T("Payable"), c.Bank, c.PartyName, c.DueDate.Date, Math.Max(0, c.DaysOverdue), AgingLabel(c.Bucket), c.Amount);
            return x.ToFile(format);
        }

        return View(model);
    }

    // ================================================================== helpers

    [NonAction]
    public string AgingLabel(string key) => key switch
    {
        "notdue" => T("Not due yet"),
        "90+" => T("Over 90 days late"),
        _ => string.Format(T("{0} days late"), key)
    };

    private static string WeekdayName(int day) =>
        System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.GetDayName((DayOfWeek)day);

    private ReportExport NewExport(string fileName, string title, ReportPageViewModel model)
    {
        var store = _store.CurrentValue;
        var x = new ReportExport
        {
            FileName = fileName,
            Title = title,
            StoreName = store.NameFor(IsAr ? "ar" : "en"),
            RightToLeft = IsAr
        };
        if (model.HasDateRange)
        {
            x.Header.Add($"{T("From")} {model.From:yyyy-MM-dd} {T("To")} {model.To:yyyy-MM-dd}");
        }

        x.Header.Add($"{T("Printed")} {DateTime.Now:yyyy-MM-dd HH:mm} · {User.Identity?.Name}");
        return x;
    }

    private CashOutsReportFilterDto DateFilter(DateTime f, DateTime t) => new() { FromUtc = ToUtc(f), ToUtc = ToUtc(t.AddDays(1)) };

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
        var all = await _categories.GetAllAsync(includeInactive: false, cancellationToken);
        return all
            .OrderBy(x => IsAr ? (x.ParentNameAr ?? x.NameAr) : (x.ParentNameEn ?? x.NameEn))
            .ThenBy(x => x.ParentCategoryId.HasValue)
            .ThenBy(x => IsAr ? x.NameAr : x.NameEn)
            .Select(x => new ReportOption
            {
                Id = x.Id,
                Name = x.ParentCategoryId.HasValue
                    ? (IsAr ? $"{x.ParentNameAr} › {x.NameAr}" : $"{x.ParentNameEn} › {x.NameEn}")
                    : (IsAr ? x.NameAr : x.NameEn)
            })
            .ToList();
    }
}
