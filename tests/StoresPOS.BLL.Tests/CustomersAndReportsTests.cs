using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

/// <summary>Phase 10: customers (+ POS F4), reports, shift history and the dashboard.</summary>
public class CustomersAndReportsTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class Ctx : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required ShiftService Shifts { get; init; }
        public required SalesService Sales { get; init; }
        public required CashOutService CashOuts { get; init; }
        public required ReturnService Returns { get; init; }
        public required CustomerService Customers { get; init; }
        public required ReportService Reports { get; init; }
        public required DashboardService Dashboard { get; init; }
        public required Clock Clock { get; init; }
        public int Cashier { get; init; }
        public int Manager { get; init; }
        public int Oil { get; init; }
        public int Tea { get; init; }
        public int Salt { get; init; }
        public int OilsCategory { get; init; }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    // ------------------------------------------------------------ customers

    [Fact]
    public async Task Customer_Create_TrimsAndValidates()
    {
        await using var c = await CreateAsync();
        var created = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "  Ahmed Ali ", Phone = " 0100 ", DefaultSaleType = SaleType.Wholesale });
        Assert.Equal("Ahmed Ali", created.Name);
        Assert.Equal("0100", created.Phone);
        Assert.Equal(SaleType.Wholesale, created.DefaultSaleType);

        var noName = await Assert.ThrowsAsync<BusinessException>(() => c.Customers.CreateAsync(new CustomerUpsertDto { Name = " " }));
        Assert.Equal(CustomerService.MsgNameRequired, noName.MessageKey);

        var dupe = await Assert.ThrowsAsync<BusinessException>(() => c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Other", Phone = "0100" }));
        Assert.Equal(CustomerService.MsgPhoneTaken, dupe.MessageKey);

        // Editing the same customer keeps its own phone.
        await c.Customers.UpdateAsync(created.Id, new CustomerUpsertDto { Name = "Ahmed", Phone = "0100" });
    }

    [Fact]
    public async Task Customer_SearchPage_PagesByName_AndClampsThePage()
    {
        await using var c = await CreateAsync();
        for (var i = 1; i <= 14; i++)
        {
            await c.Customers.CreateAsync(new CustomerUpsertDto { Name = $"Client {i:00}", Phone = $"0102000{i:0000}" });
        }

        var first = await c.Customers.SearchPageAsync(null, 1, 12);
        Assert.Equal(14, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(12, first.Items.Count);
        Assert.Equal("Client 01", first.Items[0].Name);

        var last = await c.Customers.SearchPageAsync("Client", 99, 12);
        Assert.Equal(2, last.Page);
        Assert.Equal(new[] { "Client 13", "Client 14" }, last.Items.Select(x => x.Name));

        var byPhone = await c.Customers.SearchPageAsync("01020000007", 1, 12);
        Assert.Equal("Client 07", Assert.Single(byPhone.Items).Name);
    }
    [Fact]
    public async Task Customer_Search_ByNameOrPhone_ActiveOnly()
    {
        await using var c = await CreateAsync();
        var ahmed = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Ahmed", Phone = "01001112222" });
        await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Sara", Phone = "01200000000" });
        var gone = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Ahmed Old" });
        await c.Customers.DeactivateAsync(gone.Id);

        Assert.Equal(ahmed.Id, Assert.Single(await c.Customers.SearchAsync("Ahm")).Id);
        Assert.Equal("Sara", Assert.Single(await c.Customers.SearchAsync("0120")).Name);
        Assert.Equal(2, (await c.Customers.GetListAsync()).Count);
        Assert.Equal(3, (await c.Customers.GetListAsync(new CustomerListFilterDto { IncludeInactive = true })).Count);
    }

    [Fact]
    public async Task SetCustomer_SwitchesToDefaultSaleType_AndInvoiceKeepsCustomer()
    {
        await using var c = await CreateAsync();
        var shop = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Corner shop", Phone = "0111", DefaultSaleType = SaleType.Wholesale });

        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 2m);
        var cart = await c.Sales.SetCustomerAsync(c.Cashier, shop.Id);

        Assert.Equal(shop.Id, cart.CustomerId);
        Assert.Equal("Corner shop", cart.CustomerName);
        Assert.Equal(SaleType.Wholesale, cart.SaleType);
        Assert.Equal(144m, cart.Total);   // 2 × 72 wholesale

        var sale = await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 200m });
        var invoice = (await c.Sales.GetInvoiceAsync(sale.InvoiceId))!;
        Assert.Equal("Corner shop", invoice.CustomerName);
        Assert.Equal("0111", invoice.CustomerPhone);

        // The next sale starts as walk-in.
        Assert.Null((await c.Sales.GetCartAsync(c.Cashier)).CustomerId);

        var details = (await c.Customers.GetByIdAsync(shop.Id))!;
        Assert.Equal(1, details.InvoiceCount);
        Assert.Equal(144m, details.SalesTotal);
        Assert.Single(await c.Sales.GetInvoicesAsync(new SalesInvoiceFilterDto { CustomerId = shop.Id }));
    }

    [Fact]
    public async Task SetCustomer_BackToWalkIn_AndClearResetsCustomer()
    {
        await using var c = await CreateAsync();
        var ahmed = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Ahmed" });

        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        await c.Sales.SetCustomerAsync(c.Cashier, ahmed.Id);
        Assert.Null((await c.Sales.SetCustomerAsync(c.Cashier, null)).CustomerId);

        await c.Sales.SetCustomerAsync(c.Cashier, ahmed.Id);
        Assert.Null((await c.Sales.ClearAsync(c.Cashier)).CustomerId);
    }

    [Fact]
    public async Task SetCustomer_InactiveOrMissing_IsRejected()
    {
        await using var c = await CreateAsync();
        var old = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Old" });
        await c.Customers.DeactivateAsync(old.Id);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Sales.SetCustomerAsync(c.Cashier, old.Id));
        Assert.Equal(SalesService.MsgCustomerNotFound, ex.MessageKey);
        await Assert.ThrowsAsync<BusinessException>(() => c.Sales.SetCustomerAsync(c.Cashier, 9999));
    }

    [Fact]
    public async Task Customer_Totals_SubtractReturns()
    {
        await using var c = await CreateAsync();
        var ahmed = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Ahmed" });
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 2m);
        await c.Sales.SetCustomerAsync(c.Cashier, ahmed.Id);
        var sale = (await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 200m })).InvoiceId;

        await ReturnAsync(c, sale, c.Oil, 1m);

        var details = (await c.Customers.GetByIdAsync(ahmed.Id))!;
        Assert.Equal(156m, details.SalesTotal);
        Assert.Equal(78m, details.ReturnedTotal);
        Assert.Equal(78m, details.NetTotal);
        Assert.Single(await c.Returns.GetListAsync(new SalesReturnFilterDto { CustomerId = ahmed.Id }));
    }

    // ------------------------------------------------------------ sales report

    [Fact]
    public async Task SalesReport_SharesDiscount_CountsReturns_AndProfit()
    {
        await using var c = await CreateAsync();

        // Manager: 2 oil + 1 tea = 218, discount 18 → 200 cash.
        await c.Sales.AddProductAsync(c.Manager, c.Oil, 2m);
        await c.Sales.AddProductAsync(c.Manager, c.Tea, 1m);
        await c.Sales.SetDiscountAsync(c.Manager, new DiscountDto { Amount = 18m });
        var big = (await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 200m })).InvoiceId;

        // Cashier: 1 tea by card = 62.
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card });

        // One oil back from the discounted invoice: 78 × 200/218 = 71.56.
        await ReturnAsync(c, big, c.Oil, 1m);

        var r = await c.Reports.GetSalesAsync(new SalesReportFilterDto());
        Assert.Equal(2, r.InvoiceCount);
        Assert.Equal(280m, r.GrossSales);
        Assert.Equal(262m, r.NetSales);
        Assert.Equal(18m, r.Discounts);
        Assert.Equal(1, r.ReturnsCount);
        Assert.Equal(71.56m, r.ReturnsAmount);
        Assert.Equal(165m, r.Cost);                 // 2×65 + 50 + 50 − 65 returned
        Assert.Equal(25.44m, r.Profit);              // 262 − 71.56 − 165

        Assert.Equal(200m, r.ByMethod.Single(x => x.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(62m, r.ByMethod.Single(x => x.Method == PaymentMethod.Card).Amount);
        Assert.Equal(200m, r.ByCashier.Single(x => x.Id == c.Manager).Amount);
        Assert.Equal(r.NetSales, r.ByDay.Sum(x => x.Amount));
        Assert.Equal(c.Oil, r.TopProducts[0].ProductId);   // oil 143.12 > tea 56.88 + 62
        Assert.Equal(143.12m, r.TopProducts[0].Amount);
        Assert.Equal(r.NetSales, r.TopProducts.Sum(x => x.Amount));
    }

    [Fact]
    public async Task SalesReport_Filters_ByCategory_Cashier_AndMethod()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 1m);
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 200m });
        await c.Sales.AddProductAsync(c.Manager, c.Tea, 1m);
        await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.InstaPay });

        var oils = await c.Reports.GetSalesAsync(new SalesReportFilterDto { CategoryId = c.OilsCategory });
        Assert.Equal(1, oils.InvoiceCount);
        Assert.Equal(78m, oils.NetSales);

        var cashier = await c.Reports.GetSalesAsync(new SalesReportFilterDto { CashierUserId = c.Cashier });
        Assert.Equal(140m, cashier.NetSales);

        var insta = await c.Reports.GetSalesAsync(new SalesReportFilterDto { PaymentMethod = PaymentMethod.InstaPay });
        Assert.Equal(62m, insta.NetSales);

        var tomorrow = c.Clock.Now.UtcDateTime.Date.AddDays(1);
        var none = await c.Reports.GetSalesAsync(new SalesReportFilterDto { FromUtc = tomorrow, ToUtc = tomorrow.AddDays(1) });
        Assert.Equal(0, none.InvoiceCount);
    }

    // ------------------------------------------------------------ customer type

    [Fact]
    public async Task CustomerType_IsSaved_SeparateFromPriceLevel_AndFilters()
    {
        await using var c = await CreateAsync();
        var shop = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Corner shop", CustomerType = CustomerType.Wholesale });
        var firm = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Nile Co", CustomerType = CustomerType.Company, DefaultSaleType = SaleType.Wholesale });
        var mona = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Mona" });

        Assert.Equal(CustomerType.Wholesale, shop.CustomerType);
        Assert.Equal(SaleType.Retail, shop.DefaultSaleType);          // the type does not force the price level
        Assert.Equal(CustomerType.Company, firm.CustomerType);
        Assert.Equal(SaleType.Wholesale, firm.DefaultSaleType);
        Assert.Equal(CustomerType.Retail, mona.CustomerType);          // default

        var companies = await c.Customers.GetListAsync(new CustomerListFilterDto { CustomerType = CustomerType.Company });
        Assert.Equal("Nile Co", Assert.Single(companies).Name);
        Assert.Equal(3, (await c.Customers.GetListAsync(new CustomerListFilterDto())).Count);

        await c.Customers.UpdateAsync(mona.Id, new CustomerUpsertDto { Name = "Mona", CustomerType = CustomerType.Company });
        Assert.Equal(2, (await c.Customers.GetListAsync(new CustomerListFilterDto { CustomerType = CustomerType.Company })).Count);

        // Undefined values fall back to Retail; the POS picker carries the type.
        var odd = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Odd", CustomerType = (CustomerType)9 });
        Assert.Equal(CustomerType.Retail, odd.CustomerType);
        var page = await c.Customers.SearchPageAsync("Nile", 1, 12);
        Assert.Equal(CustomerType.Company, Assert.Single(page.Items).CustomerType);
    }

    [Fact]
    public async Task SalesReport_GroupsAndFilters_ByCustomerType()
    {
        await using var c = await CreateAsync();
        var shop = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Corner shop", CustomerType = CustomerType.Wholesale });
        var firm = await c.Customers.CreateAsync(new CustomerUpsertDto { Name = "Nile Co", CustomerType = CustomerType.Company });

        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 1m);                 // 78 → wholesale-type customer (retail prices)
        await c.Sales.SetCustomerAsync(c.Cashier, shop.Id);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 100m });

        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);                 // 62 → company
        await c.Sales.SetCustomerAsync(c.Cashier, firm.Id);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card });

        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);                 // 62 → walk-in
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card });

        var r = await c.Reports.GetSalesAsync(new SalesReportFilterDto());
        Assert.Equal(78m, r.ByCustomerType.Single(x => x.Id == (int)CustomerType.Wholesale).Amount);
        Assert.Equal(62m, r.ByCustomerType.Single(x => x.Id == (int)CustomerType.Company).Amount);
        Assert.Equal(62m, r.ByCustomerType.Single(x => x.Id == SalesReportFilterDto.WalkInGroup).Amount);
        Assert.DoesNotContain(r.ByCustomerType, x => x.Id == (int)CustomerType.Retail);
        Assert.Equal(r.NetSales, r.ByCustomerType.Sum(x => x.Amount));
        Assert.Equal(new[] { "Corner shop", "Nile Co" }, r.TopCustomers.Select(x => x.Name));

        Assert.Equal(62m, (await c.Reports.GetSalesAsync(new SalesReportFilterDto { CustomerGroup = (int)CustomerType.Company })).NetSales);
        Assert.Equal(62m, (await c.Reports.GetSalesAsync(new SalesReportFilterDto { CustomerGroup = SalesReportFilterDto.WalkInGroup })).NetSales);
        Assert.Equal(0, (await c.Reports.GetSalesAsync(new SalesReportFilterDto { CustomerGroup = (int)CustomerType.Retail })).InvoiceCount);

        // Grouping follows the customer's CURRENT type.
        await c.Customers.UpdateAsync(firm.Id, new CustomerUpsertDto { Name = "Nile Co", CustomerType = CustomerType.Retail });
        var after = await c.Reports.GetSalesAsync(new SalesReportFilterDto());
        Assert.Equal(62m, after.ByCustomerType.Single(x => x.Id == (int)CustomerType.Retail).Amount);
    }

    // ------------------------------------------------------------ other reports

    [Fact]
    public async Task PurchasesReport_GroupsBySupplierAndProduct_ConfirmedOnly()
    {
        await using var c = await CreateAsync();
        var acme = new Supplier { NameAr = "أكمي", NameEn = "Acme" };
        c.Db.Suppliers.Add(acme);
        await c.Db.SaveChangesAsync();
        var day = c.Clock.Now.UtcDateTime.Date;

        c.Db.PurchaseInvoices.AddRange(
            Purchase(acme.Id, "P-1", day, PurchaseInvoiceStatus.Confirmed, c.Manager, (c.Oil, 10m, 60m)),
            Purchase(acme.Id, "P-2", day, PurchaseInvoiceStatus.Confirmed, c.Manager, (c.Oil, 5m, 64m), (c.Tea, 2m, 50m)),
            Purchase(acme.Id, "P-3", day, PurchaseInvoiceStatus.Draft, c.Manager, (c.Tea, 100m, 1m)));
        await c.Db.SaveChangesAsync();

        var r = await c.Reports.GetPurchasesAsync(new PurchasesReportFilterDto { FromUtc = day, ToUtc = day.AddDays(1) });
        Assert.Equal(2, r.InvoiceCount);
        Assert.Equal(1020m, r.Total);                       // 600 + 320 + 100
        Assert.Equal(1020m, Assert.Single(r.BySupplier).Amount);
        var oil = r.ByProduct.Single(x => x.Id == c.Oil);
        Assert.Equal(15m, oil.Quantity);
        Assert.Equal(920m, oil.Amount);
    }

    [Fact]
    public async Task InventoryReport_ValuesStockAtCost_AndSummarisesMovements()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 2m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 200m });

        var r = await c.Reports.GetInventoryAsync(new InventoryReportFilterDto());
        Assert.Equal(3, r.ProductCount);
        Assert.Equal(8m * 65m + 5m * 50m + 1m * 10m, r.TotalCostValue);
        var sale = r.Movements.Single(x => x.Type == StockMovementType.Sale);
        Assert.Equal(2m, sale.QuantityOut);

        var low = await c.Reports.GetInventoryAsync(new InventoryReportFilterDto { LowStockOnly = true });
        Assert.Equal(c.Salt, Assert.Single(low.Rows).ProductId);
        Assert.Equal(1, low.LowStockCount);
    }

    [Fact]
    public async Task CashOutsReport_GroupsReasonsIgnoringCase()
    {
        await using var c = await CreateAsync();
        await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 10m, Reason = "Tea" }, c.Cashier);
        await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 5m, Reason = " tea " }, c.Manager);
        await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 20m, Reason = "Cleaning" }, c.Cashier);

        var r = await c.Reports.GetCashOutsAsync(new CashOutsReportFilterDto());
        Assert.Equal(35m, r.Total);
        Assert.Equal(2, r.ByReason.Count);
        Assert.Equal(15m, r.ByReason.Single(x => x.Name.Equals("tea", StringComparison.OrdinalIgnoreCase)).Amount);
        Assert.Equal(30m, r.ByUser.Single(x => x.Id == c.Cashier).Amount);

        var mine = await c.Reports.GetCashOutsAsync(new CashOutsReportFilterDto { UserId = c.Manager });
        Assert.Equal(5m, mine.Total);
    }

    // ------------------------------------------------------------ shift history

    [Fact]
    public async Task ShiftHistory_FiltersByResult_AndShowsLiveExpectedForOpenShift()
    {
        await using var c = await CreateAsync();
        var first = (await c.Shifts.GetOpenShiftAsync())!.Id;
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 62m });

        // Expected 100 + 62 = 162; counted 150 → shortage of 12.
        await c.Shifts.CloseAsync(new CloseShiftDto { ShiftId = first, ActualCash = 150m, HandoverTreasuryId = 1 }, c.Manager);
        c.Clock.Now = c.Clock.Now.AddHours(1);
        await c.Shifts.OpenAsync(40m, c.Manager);

        var all = await c.Shifts.GetHistoryAsync();
        Assert.Equal(2, all.Count);
        var open = all[0];
        Assert.Equal(ShiftStatus.Open, open.Status);
        Assert.Equal(40m, open.ExpectedCash);

        var shortage = Assert.Single(await c.Shifts.GetHistoryAsync(new ShiftHistoryFilterDto { Result = ShiftResultFilter.Shortage }));
        Assert.Equal(first, shortage.Id);
        Assert.Equal(-12m, shortage.CashDifference);
        Assert.Equal(162m, shortage.ExpectedCash);
        Assert.Equal(62m, shortage.SalesTotal);
        Assert.Contains("Mona", shortage.Participants);

        // The cashier only worked in the first shift.
        Assert.Equal(first, Assert.Single(await c.Shifts.GetHistoryAsync(new ShiftHistoryFilterDto { UserId = c.Cashier })).Id);

        var report = await c.Reports.GetShiftsAsync(new ShiftHistoryFilterDto());
        Assert.Equal(-12m, report.ShortageTotal);
        Assert.Equal(62m, report.ByCashier.Single(x => x.Id == c.Cashier).Amount);
    }

    // ------------------------------------------------------------ dashboard

    [Fact]
    public async Task Dashboard_ShowsToday_OpenShift_LowStock_AndRecentActivity()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 100m });
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        var card = (await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card })).InvoiceId;
        await ReturnAsync(c, card, c.Tea, 1m, PaymentMethod.Card);
        await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 10m, Reason = "Tea" }, c.Cashier);

        var d = await c.Dashboard.GetAsync();
        Assert.Equal(140m, d.TodaySales);
        Assert.Equal(78m, d.TodayCash);
        Assert.Equal(62m, d.TodayNonCash);
        Assert.Equal(2, d.TodayInvoiceCount);
        Assert.Equal(1, d.TodayReturnsCount);
        Assert.Equal(62m, d.TodayReturnsAmount);
        Assert.Equal(10m, d.TodayCashOuts);
        Assert.NotNull(d.OpenShift);
        Assert.Equal(1, d.LowStockCount);
        Assert.Equal(c.Salt, Assert.Single(d.LowStockTop).ProductId);
        Assert.Equal(2, d.RecentSales.Count);
        Assert.Single(d.RecentCashOuts);

        // Yesterday's figures are not today's.
        c.Clock.Now = c.Clock.Now.AddDays(1);
        Assert.Equal(0m, (await c.Dashboard.GetAsync()).TodaySales);
    }

    [Fact]
    public async Task DashboardPeriods_CompareWithThePeriodBefore_AndBucketByHourOrDay()
    {
        await using var c = await CreateAsync();   // clock: 2026-11-19 10:00 (UTC store)
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 100m });
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card });
        c.Clock.Now = c.Clock.Now.AddDays(1);      // now 2026-11-20

        var today = await c.Dashboard.GetOverviewAsync(DashboardPeriod.Today);
        Assert.Equal(0m, today.NetSales.Value);
        Assert.Equal(140m, today.NetSales.Previous);
        Assert.Equal(-100m, today.NetSales.ChangePercent);
        Assert.Equal(2m, today.Invoices.Previous);
        Assert.NotNull(today.Alerts.OldOpenShiftId);   // yesterday's shift still open

        var yesterday = await c.Dashboard.GetOverviewAsync(DashboardPeriod.Yesterday);
        Assert.Equal(140m, yesterday.NetSales.Value);
        Assert.Null(yesterday.NetSales.ChangePercent);   // nothing the day before
        Assert.Equal(70m, yesterday.AverageInvoice.Value);
        Assert.Equal(140m, yesterday.NetSales.Trend.Sum());

        var hourly = await c.Dashboard.GetChartsAsync(DashboardPeriod.Yesterday);
        Assert.True(hourly.Hourly);
        Assert.Equal(140m, hourly.Current[hourly.Labels.ToList().IndexOf("10")]);
        Assert.Equal(2, hourly.PaymentMethods.Count);
        Assert.Equal(78m, hourly.PaymentMethods[0].Amount);   // biggest slice first

        var week = await c.Dashboard.GetChartsAsync(DashboardPeriod.Last7Days);
        Assert.False(week.Hourly);
        Assert.Equal(7, week.Labels.Count);
        Assert.Equal(140m, week.Current[week.Labels.ToList().IndexOf("11/19")]);

        var details = await c.Dashboard.GetDetailsAsync(DashboardPeriod.Yesterday);
        Assert.Equal(c.Oil, details.TopProducts[0].ProductId);
        Assert.Equal(78m, details.TopProducts[0].Sales);
        var cashier = Assert.Single(details.Cashiers);
        Assert.Equal(2, cashier.Invoices);
        Assert.Equal(140m, cashier.Sales);
        Assert.Equal(DashboardActivityKind.Sale, details.Activity[0].Kind);
        Assert.Equal(c.Salt, Assert.Single(details.LowStock).ProductId);

        var month = c.Dashboard.GetRange(DashboardPeriod.ThisMonth);
        Assert.Equal(new DateTime(2026, 11, 1), month.FromLocal);
        Assert.Equal(new DateTime(2026, 11, 21), month.ToLocal);
        Assert.Equal(new DateTime(2026, 10, 1), month.PreviousFromLocal);
        Assert.Equal(new DateTime(2026, 10, 21), month.PreviousToLocal);

        var last30 = c.Dashboard.GetRange(DashboardPeriod.Last30Days);
        Assert.Equal(30, (last30.ToLocal - last30.FromLocal).Days);
        Assert.Equal(last30.FromLocal, last30.PreviousToLocal);
    }

    // ------------------------------------------------------------ Phase 23 reports

    /// <summary>
    /// Known data (2026-11-19, a Thursday, 10:00 UTC store):
    /// manager sells oil 78 → price changed to 70 → 10 discount → 60 cash; cashier sells tea 62 by card;
    /// manager refunds the tea (62) by card.
    /// </summary>
    private static async Task<(int OilInvoice, int TeaInvoice)> SeedPhase23Async(Ctx c)
    {
        var cart = await c.Sales.AddProductAsync(c.Manager, c.Oil, 1m);
        await c.Sales.OverridePriceAsync(c.Manager, cart.Lines[0].Id, 70m);
        await c.Sales.SetDiscountAsync(c.Manager, new DiscountDto { Amount = 10m });
        var oil = (await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 100m })).InvoiceId;
        await c.Sales.AddProductAsync(c.Cashier, c.Tea, 1m);
        var tea = (await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card })).InvoiceId;
        await ReturnAsync(c, tea, c.Tea, 1m, PaymentMethod.Card);
        return (oil, tea);
    }

    private static CashOutsReportFilterDto Day19 => new()
    {
        FromUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
        ToUtc = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task DiscountAudit_CountsDiscountsPriceChangesAndReturnsPerPerson()
    {
        await using var c = await CreateAsync();
        await SeedPhase23Async(c);

        var r = await c.Reports.GetDiscountAuditAsync(Day19);
        var manager = r.Users.Single(x => x.UserId == c.Manager);
        Assert.Equal(1, manager.Invoices);
        Assert.Equal(1, manager.DiscountCount);
        Assert.Equal(10m, manager.DiscountAmount);
        Assert.Equal(1, manager.OverrideLines);
        Assert.Equal(8m, manager.OverrideAmount);          // 78 catalog − 70 charged
        Assert.Equal(1, manager.ReturnsCount);
        Assert.Equal(62m, manager.ReturnsAmount);
        Assert.Equal(1, r.Users.Single(x => x.UserId == c.Cashier).Invoices);
        Assert.Equal(78m, Assert.Single(r.Overrides).CatalogPrice);
    }

    [Fact]
    public async Task SalesByHour_PutsSalesInTheirLocalHourAndWeekday()
    {
        await using var c = await CreateAsync();
        await SeedPhase23Async(c);

        var r = await c.Reports.GetSalesTimeAsync(Day19);
        Assert.Equal(2, r.ByHour[10].Count);
        Assert.Equal(122m, r.ByHour[10].Amount);            // 60 + 62
        Assert.Equal(122m, r.ByWeekday.Single(x => x.Id == (int)DayOfWeek.Thursday).Amount);
        var thursday = Array.IndexOf(ReportService.WeekOrder, DayOfWeek.Thursday);
        Assert.Equal(122m, r.Heat[thursday][10]);
        Assert.Equal(r.Total, r.Heat.SelectMany(x => x).Sum());
    }

    [Fact]
    public async Task Profit_IsNetOfDiscountsAndReturns_WithTheCostSavedOnTheLine()
    {
        await using var c = await CreateAsync();
        await SeedPhase23Async(c);

        var r = await c.Reports.GetProfitAsync(Day19);
        Assert.Equal(60m, r.Sales);                         // 60 + 62 − 62 refunded
        Assert.Equal(65m, r.Cost);                          // oil 65 + tea 50 − tea 50 returned
        Assert.Equal(-5m, r.Profit);
        var tea = r.ByProduct.Single(x => x.Id == c.Tea);
        Assert.Equal(0m, tea.Sales);
        Assert.Equal(0m, tea.Quantity);
        Assert.Equal(r.Sales, r.ByCategory.Sum(x => x.Sales));
        Assert.Equal(r.Profit, r.ByDay.Sum(x => x.Profit));
    }

    [Fact]
    public async Task Vat_IsIncludedVatOfEachInvoiceLessReturns()
    {
        await using var c = await CreateAsync();
        await SeedPhase23Async(c);

        var r = await c.Reports.GetTaxAsync(Day19, 14m, pricesIncludeVat: true);
        Assert.Equal(7.37m + 7.61m, r.SalesVat);            // 60 − 60/1.14 and 62 − 62/1.14
        Assert.Equal(7.61m, r.ReturnsVat);
        Assert.Equal(7.37m, r.NetVat);
        Assert.Equal(122m * 14m / 100m - 62m * 14m / 100m, (await c.Reports.GetTaxAsync(Day19, 14m, pricesIncludeVat: false)).NetVat);
    }

    [Fact]
    public async Task ProductPerformance_RanksSellers_AndFindsStockThatStoppedSelling()
    {
        await using var c = await CreateAsync();
        await SeedPhase23Async(c);

        var r = await c.Reports.GetProductPerformanceAsync(Day19, deadDays: 90);
        Assert.Equal(2, r.ProductsSold);
        Assert.Equal(c.Oil, r.Best[0].ProductId);           // line total 70 vs 62
        Assert.DoesNotContain(r.Dead, x => x.ProductId == c.Oil);

        c.Clock.Now = c.Clock.Now.AddDays(100);
        var later = await c.Reports.GetProductPerformanceAsync(Day19, deadDays: 90);
        Assert.Contains(later.Dead, x => x.ProductId == c.Oil && x.DaysSinceSale == 100);
        Assert.Contains(later.Dead, x => x.ProductId == c.Tea);
    }

    [Fact]
    public async Task Drawer_CountsEachShiftForEveryoneWhoWorkedIt()
    {
        await using var c = await CreateAsync();
        var shift = await c.Db.Shifts.SingleAsync();
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = c.Clock.Now.UtcDateTime;
        shift.ClosedByUserId = c.Manager;
        shift.CashDifference = -20m;
        shift.Reconciliation = ShiftReconciliation.Shortage;
        await c.Db.SaveChangesAsync();

        var r = await c.Reports.GetDrawerAsync(Day19);
        Assert.Equal(2, r.Users.Count);
        Assert.All(r.Users, u => { Assert.Equal(1, u.ShortageCount); Assert.Equal(-20m, u.ShortageTotal); Assert.Equal(100m, u.ShortagePercent); });
        Assert.Equal(-20m, r.ShortageTotal);
        Assert.Equal(2, Assert.Single(r.Shifts).Participants.Count);
    }

    [Fact]
    public async Task ChequeAging_PutsPendingChequesInAgeBuckets()
    {
        await using var c = await CreateAsync();
        var today = c.Clock.Now.UtcDateTime.Date;
        c.Db.Cheques.AddRange(
            new Cheque { Number = "A1", Amount = 100m, Direction = ChequeDirection.In, DueDate = today.AddDays(-40), CreatedByUserId = c.Manager },
            new Cheque { Number = "A2", Amount = 50m, Direction = ChequeDirection.Out, DueDate = today.AddDays(5), CreatedByUserId = c.Manager },
            new Cheque { Number = "A3", Amount = 30m, Direction = ChequeDirection.In, DueDate = today.AddDays(-200), CreatedByUserId = c.Manager },
            new Cheque { Number = "A4", Amount = 999m, Direction = ChequeDirection.In, DueDate = today.AddDays(-10), CreatedByUserId = c.Manager, Status = ChequeStatus.Collected });
        await c.Db.SaveChangesAsync();

        var r = await c.Reports.GetChequeAgingAsync();
        Assert.Equal(3, r.Rows.Count);                      // collected cheque left out
        Assert.Equal(100m, r.Buckets.Single(b => b.Key == "31-60").InAmount);
        Assert.Equal(30m, r.Buckets.Single(b => b.Key == "90+").InAmount);
        Assert.Equal(50m, r.Buckets.Single(b => b.Key == "notdue").OutAmount);
        Assert.Equal(130m, r.OverdueIn);
        Assert.Equal(0m, r.OverdueOut);
        Assert.Equal("A3", r.Rows[0].Number);              // latest first
    }

    [Fact]
    public void WeightedAverageCost_BlendsStockOnHandWithTheNewPurchase()
    {
        Assert.Equal(60m, InventoryService.WeightedAverageCost(10m, 50m, 10m, 70m));
        Assert.Equal(70m, InventoryService.WeightedAverageCost(0m, 50m, 10m, 70m));    // nothing on hand
        Assert.Equal(53.3333m, InventoryService.WeightedAverageCost(20m, 50m, 10m, 60m));
    }

    // ------------------------------------------------------------ fixture

    private static async Task ReturnAsync(Ctx c, int invoiceId, int productId, decimal qty, PaymentMethod method = PaymentMethod.Cash)
    {
        var invoice = (await c.Returns.GetReturnableAsync(invoiceId))!;
        var line = invoice.Lines.First(x => x.ProductId == productId);
        await c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = invoiceId,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = line.SalesInvoiceItemId, Quantity = qty }],
            RefundMethod = method,
            Reason = "Test"
        }, c.Manager);
    }

    private static PurchaseInvoice Purchase(
        int supplierId, string number, DateTime date, PurchaseInvoiceStatus status, int userId,
        params (int ProductId, decimal Qty, decimal Cost)[] lines)
    {
        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = number,
            SupplierId = supplierId,
            InvoiceDate = date,
            Status = status,
            CreatedByUserId = userId,
            ConfirmedAtUtc = status == PurchaseInvoiceStatus.Confirmed ? date : null
        };
        foreach (var (productId, qty, cost) in lines)
        {
            invoice.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = productId,
                ProductNameEn = "p" + productId,
                ProductNameAr = "p" + productId,
                Quantity = qty,
                UnitCost = cost,
                LineTotal = qty * cost
            });
        }

        invoice.SubTotal = invoice.Items.Sum(x => x.LineTotal);
        return invoice;
    }

    private static async Task<Ctx> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options);

        var cashierRole = new Role { Name = AppRoles.Cashier };
        var managerRole = new Role { Name = AppRoles.Manager };
        db.Roles.AddRange(cashierRole, managerRole);
        await db.SaveChangesAsync();
        db.RolePermissions.AddRange(AppPermissions.DefaultsFor(AppRoles.Cashier)
            .Select(p => new RolePermission { RoleId = cashierRole.Id, Permission = p }));
        db.RolePermissions.AddRange(AppPermissions.DefaultsFor(AppRoles.Manager)
            .Select(p => new RolePermission { RoleId = managerRole.Id, Permission = p }));

        var mona = new User { Username = "mona", DisplayName = "Mona", PasswordHash = "h", PasswordSalt = "s", RoleId = cashierRole.Id };
        var omar = new User { Username = "omar", DisplayName = "Omar", PasswordHash = "h", PasswordSalt = "s", RoleId = managerRole.Id };
        db.Users.AddRange(mona, omar);

        var oils = new Category { NameAr = "زيوت", NameEn = "Oils" };
        db.Categories.Add(oils);
        await db.SaveChangesAsync();

        var oil = new Product
        {
            NameAr = "زيت", NameEn = "Oil", Barcode = "622100", Unit = "L", CategoryId = oils.Id,
            PurchasePrice = 65m, RetailPrice = 78m, WholesalePrice = 72m,
            StockQuantity = 10m, LowStockThreshold = 2m
        };
        var tea = new Product
        {
            NameAr = "شاي", NameEn = "Tea", Barcode = "622200", Unit = "pcs",
            PurchasePrice = 50m, RetailPrice = 62m, WholesalePrice = 58m,
            StockQuantity = 5m, LowStockThreshold = 1m
        };
        var salt = new Product
        {
            NameAr = "ملح", NameEn = "Salt", Barcode = "622300", Unit = "pcs",
            PurchasePrice = 10m, RetailPrice = 12m, WholesalePrice = 11m,
            StockQuantity = 1m, LowStockThreshold = 3m
        };
        db.Products.AddRange(oil, tea, salt);
        await db.SaveChangesAsync();

        var clock = new Clock(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var inventory = TestFactories.Inventory(db);
        var warehouses = TestFactories.Warehouses(db, clock);
        await warehouses.EnsureSeededAsync();
        var permissions = new PermissionService(db);
        var paymentMethods = new PaymentMethodService(db);
        await paymentMethods.EnsureSeededAsync();
        var treasuries = new TreasuryService(db, clock);
        await treasuries.EnsureSeededAsync();
        var shifts = new ShiftService(db, treasuries, clock);
        var sales = new SalesService(db, shifts, inventory, permissions, paymentMethods, treasuries, new ChequeService(db, treasuries, clock), warehouses, TestFactories.StoreMonitor(), clock);
        var cashOuts = new CashOutService(db, shifts, clock);

        await shifts.OpenAsync(100m, mona.Id);
        await shifts.ResumeAsync(omar.Id);

        return new Ctx
        {
            Db = db,
            Shifts = shifts,
            Sales = sales,
            CashOuts = cashOuts,
            Returns = new ReturnService(db, inventory, permissions, paymentMethods, warehouses, TestFactories.StoreMonitor(), clock),
            Customers = new CustomerService(db, clock),
            Reports = new ReportService(db, shifts, cashOuts, clock),
            Dashboard = new DashboardService(db, shifts, sales, cashOuts, treasuries, new ChequeService(db, treasuries, clock), clock),
            Clock = clock,
            Cashier = mona.Id,
            Manager = omar.Id,
            Oil = oil.Id,
            Tea = tea.Id,
            Salt = salt.Id,
            OilsCategory = oils.Id
        };
    }
}
