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
        await c.Shifts.CloseAsync(new CloseShiftDto { ShiftId = first, ActualCash = 150m }, c.Manager);
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
        var inventory = new InventoryService(db);
        var permissions = new PermissionService(db);
        var shifts = new ShiftService(db, clock);
        var sales = new SalesService(db, shifts, inventory, permissions, clock);
        var cashOuts = new CashOutService(db, shifts, clock);

        await shifts.OpenAsync(100m, mona.Id);
        await shifts.ResumeAsync(omar.Id);

        return new Ctx
        {
            Db = db,
            Shifts = shifts,
            Sales = sales,
            CashOuts = cashOuts,
            Returns = new ReturnService(db, inventory, permissions, Options.Create(new StoreSettings()), clock),
            Customers = new CustomerService(db, clock),
            Reports = new ReportService(db, shifts, cashOuts, clock),
            Dashboard = new DashboardService(db, shifts, sales, cashOuts, clock),
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
