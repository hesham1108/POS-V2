using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class SalesServiceTests
{
    private sealed class Ctx : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required ShiftService Shifts { get; init; }
        public required SalesService Sales { get; init; }
        public int Cashier { get; init; }
        public int Manager { get; init; }
        public int Oil { get; init; }
        public int Tea { get; init; }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task NotInShift_CannotSell()
    {
        await using var c = await CreateAsync(openShift: false);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Sales.GetCartAsync(c.Cashier));
        Assert.Equal(SalesService.MsgNotInShift, ex.MessageKey);
    }

    [Fact]
    public async Task Add_SameProductTwice_IncrementsLine_AndTotals()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var cart = await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        cart = await c.Sales.AddProductAsync(c.Cashier, c.Tea);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(2m, cart.Lines[0].Quantity);
        Assert.Equal(156m, cart.Lines[0].LineTotal);   // 2 × 78
        Assert.Equal(218m, cart.Total);                 // 156 + 62
    }

    [Fact]
    public async Task Scan_ExactBarcode_AddsProduct_UnknownThrows()
    {
        await using var c = await CreateAsync();
        var cart = await c.Sales.ScanAsync(c.Cashier, "622100");
        Assert.Single(cart.Lines);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Sales.ScanAsync(c.Cashier, "000"));
        Assert.Equal(SalesService.MsgBarcodeNotFound, ex.MessageKey);
    }

    [Fact]
    public async Task Quantity_AboveStock_IsBlocked_WithFriendlyMessage()
    {
        await using var c = await CreateAsync();
        var cart = await c.Sales.AddProductAsync(c.Cashier, c.Tea);   // tea stock = 3

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.SetQuantityAsync(c.Cashier, cart.Lines[0].Id, 4));
        Assert.Equal(SalesService.MsgNotEnoughStock, ex.MessageKey);
        Assert.Equal("Only 3 left of \"Tea\". Lower the quantity to 3 or less.", ex.Message);
    }

    [Fact]
    public async Task Quantity_Zero_RemovesLine()
    {
        await using var c = await CreateAsync();
        var cart = await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        cart = await c.Sales.ChangeQuantityAsync(c.Cashier, cart.Lines[0].Id, -1);
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public async Task Wholesale_Reprices_ButKeepsOverriddenLines()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Manager, c.Oil);
        var cart = await c.Sales.AddProductAsync(c.Manager, c.Tea);
        await c.Sales.OverridePriceAsync(c.Manager, cart.Lines[1].Id, 50m);

        cart = await c.Sales.SetSaleTypeAsync(c.Manager, SaleType.Wholesale);

        Assert.Equal(SaleType.Wholesale, cart.SaleType);
        Assert.Equal(72m, cart.Lines[0].UnitPrice);     // wholesale oil
        Assert.Equal(50m, cart.Lines[1].UnitPrice);     // override kept
        Assert.Equal(58m, cart.Lines[1].CatalogUnitPrice);
    }

    [Fact]
    public async Task PriceOverride_RequiresPermission_AndStoresBothPrices()
    {
        await using var c = await CreateAsync();
        var cart = await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.OverridePriceAsync(c.Cashier, cart.Lines[0].Id, 70m));
        Assert.Equal(SalesService.MsgNoPricePermission, ex.MessageKey);

        cart = await c.Sales.AddProductAsync(c.Manager, c.Oil);
        cart = await c.Sales.OverridePriceAsync(c.Manager, cart.Lines[0].Id, 70m);
        Assert.True(cart.Lines[0].IsPriceOverridden);
        Assert.Equal(78m, cart.Lines[0].CatalogUnitPrice);
        Assert.Equal(70m, cart.Lines[0].UnitPrice);

        var item = await c.Db.SalesInvoiceItems.SingleAsync(x => x.Id == cart.Lines[0].Id);
        Assert.Equal(c.Manager, item.PriceOverriddenByUserId);

        var product = await c.Db.Products.SingleAsync(x => x.Id == c.Oil);
        Assert.Equal(78m, product.RetailPrice); // catalog untouched (rule 6)
    }

    [Fact]
    public async Task Discount_RequiresPermission_AndCannotExceedSubtotal()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.SetDiscountAsync(c.Cashier, new DiscountDto { Amount = 5 }));

        await c.Sales.AddProductAsync(c.Manager, c.Oil);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.SetDiscountAsync(c.Manager, new DiscountDto { Amount = 100 }));
        Assert.Equal(SalesService.MsgInvalidDiscount, ex.MessageKey);

        var cart = await c.Sales.SetDiscountAsync(c.Manager, new DiscountDto { Percent = 10 });
        Assert.Equal(7.8m, cart.DiscountAmount);
        Assert.Equal(70.2m, cart.Total);
    }

    [Fact]
    public async Task Hold_ThenResume_RestoresCart_AndHeldCount()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var cart = await c.Sales.HoldAsync(c.Cashier);

        Assert.True(cart.IsEmpty);
        Assert.Equal(1, cart.HeldCount);

        var held = await c.Sales.GetHeldAsync(c.Cashier);
        var resumed = await c.Sales.ResumeHeldAsync(c.Cashier, held.Single().Id);
        Assert.Single(resumed.Lines);
        Assert.Equal(0, resumed.HeldCount);
    }

    [Fact]
    public async Task Resume_WithNonEmptyCart_Throws()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        await c.Sales.HoldAsync(c.Cashier);
        var heldId = (await c.Sales.GetHeldAsync(c.Cashier)).Single().Id;
        await c.Sales.AddProductAsync(c.Cashier, c.Tea);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Sales.ResumeHeldAsync(c.Cashier, heldId));
        Assert.Equal(SalesService.MsgFinishCurrentFirst, ex.MessageKey);
    }

    [Fact]
    public async Task Complete_Cash_GivesChange_DeductsStock_NumbersInvoice()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil, 2);
        await c.Sales.AddProductAsync(c.Cashier, c.Tea);

        var sale = await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 300 });

        Assert.Equal("S-000001", sale.InvoiceNumber);
        Assert.Equal(218m, sale.Total);
        Assert.Equal(82m, sale.ChangeAmount);

        var oil = await c.Db.Products.AsNoTracking().SingleAsync(x => x.Id == c.Oil);
        Assert.Equal(8m, oil.StockQuantity);
        var movement = await c.Db.StockMovements.AsNoTracking().FirstAsync(x => x.ProductId == c.Oil);
        Assert.Equal(StockMovementType.Sale, movement.MovementType);
        Assert.Equal(-2m, movement.QuantityChange);
        Assert.Equal(InventoryService.SaleReferenceType, movement.ReferenceType);
        Assert.Equal(sale.InvoiceId, movement.ReferenceId);

        // Fresh empty cart afterwards, next number continues.
        Assert.True((await c.Sales.GetCartAsync(c.Cashier)).IsEmpty);
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var next = await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Card });
        Assert.Equal("S-000002", next.InvoiceNumber);
        Assert.Equal(0m, next.ChangeAmount);
    }

    [Fact]
    public async Task Complete_PaidTooLow_Throws_AndKeepsCart()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 50 }));
        Assert.Equal("Paid 50.00 is less than the total 78.00.", ex.Message);
        Assert.Single((await c.Sales.GetCartAsync(c.Cashier)).Lines);
    }

    [Fact]
    public async Task Complete_EmptyInvoice_Throws()
    {
        await using var c = await CreateAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.CompleteAsync(c.Cashier, new PaymentDto()));
        Assert.Equal(SalesService.MsgEmptyInvoice, ex.MessageKey);
    }

    [Fact]
    public async Task ShiftSummary_SeparatesCashFromNonCash_PerCashier()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash });
        await c.Sales.AddProductAsync(c.Manager, c.Tea);
        await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.InstaPay });

        var summary = (await c.Shifts.GetOpenShiftAsync())!;
        Assert.Equal(78m, summary.CashSales);
        Assert.Equal(62m, summary.InstaPaySales);
        Assert.Equal(100m + 78m, summary.ExpectedCash); // opening 100 + cash only
        Assert.Equal(2, summary.InvoiceCount);
        var cashier = summary.Participants.Single(x => x.UserId == c.Cashier);
        Assert.Equal(1, cashier.InvoiceCount);
        Assert.Equal(78m, cashier.CashSales);
    }

    [Fact]
    public async Task Close_BlockedBySaleInProgress_UntilHeld_ThenHeldCancelled()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var shiftId = (await c.Shifts.GetOpenShiftAsync())!.Id;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Shifts.CloseAsync(new CloseShiftDto { ShiftId = shiftId, HandoverTreasuryId = 1 }, c.Manager));
        Assert.Equal(ShiftService.MsgSaleInProgress, ex.MessageKey);
        Assert.Equal("Mona has an invoice in progress. Wait until it is paid or held.", ex.Message);

        var draftId = (await c.Shifts.GetOpenShiftAsync())!.InProgressSales.Single().InvoiceId;
        await c.Sales.HoldForCashierAsync(c.Manager, draftId);
        Assert.Equal(1, (await c.Shifts.GetOpenShiftAsync())!.HeldInvoiceCount);

        var closed = await c.Shifts.CloseAsync(new CloseShiftDto { ShiftId = shiftId, HandoverTreasuryId = 1 }, c.Manager);
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        var status = await c.Db.SalesInvoices.AsNoTracking().Where(x => x.Id == draftId).Select(x => x.Status).SingleAsync();
        Assert.Equal(SalesInvoiceStatus.Cancelled, status);
    }

    [Fact]
    public async Task HoldForCashier_RequiresClosePermission()
    {
        await using var c = await CreateAsync();
        var cart = await c.Sales.AddProductAsync(c.Manager, c.Oil);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.Sales.HoldForCashierAsync(c.Cashier, cart.InvoiceId));
        Assert.Equal(SalesService.MsgNoClosePermission, ex.MessageKey);
    }

    [Fact]
    public async Task Invoices_KeepSalePrices_AfterCatalogChange()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Cashier, c.Oil);
        var sale = await c.Sales.CompleteAsync(c.Cashier, new PaymentDto());

        var product = await c.Db.Products.SingleAsync(x => x.Id == c.Oil);
        product.RetailPrice = 99m;
        await c.Db.SaveChangesAsync();

        var details = (await c.Sales.GetInvoiceAsync(sale.InvoiceId))!;
        Assert.Equal(78m, details.Lines.Single().UnitPrice);
        Assert.Single(await c.Sales.GetInvoicesAsync(new SalesInvoiceFilterDto { Search = "000001" }));
    }

    private static async Task<Ctx> CreateAsync(bool openShift = true)
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

        var oil = new Product
        {
            NameAr = "زيت", NameEn = "Oil", Barcode = "622100", Unit = "L",
            PurchasePrice = 65m, RetailPrice = 78m, WholesalePrice = 72m,
            StockQuantity = 10m, LowStockThreshold = 2m
        };
        var tea = new Product
        {
            NameAr = "شاي", NameEn = "Tea", Barcode = "622200", Unit = "pcs",
            PurchasePrice = 50m, RetailPrice = 62m, WholesalePrice = 58m,
            StockQuantity = 3m, LowStockThreshold = 5m
        };
        db.Products.AddRange(oil, tea);
        await db.SaveChangesAsync();

        var time = TimeProvider.System;
        var paymentMethods = new PaymentMethodService(db);
        await paymentMethods.EnsureSeededAsync();
        var treasuries = new TreasuryService(db, time);
        await treasuries.EnsureSeededAsync();
        var shifts = new ShiftService(db, treasuries, time);
        var inventory = TestFactories.Inventory(db);
        var warehouses = TestFactories.Warehouses(db, time);
        await warehouses.EnsureSeededAsync();
        var sales = new SalesService(db, shifts, inventory, new PermissionService(db), paymentMethods, treasuries, new ChequeService(db, treasuries, time), warehouses, TestFactories.StoreMonitor(), time);

        if (openShift)
        {
            await shifts.OpenAsync(100m, mona.Id);
            await shifts.ResumeAsync(omar.Id);
        }

        return new Ctx
        {
            Db = db, Shifts = shifts, Sales = sales,
            Cashier = mona.Id, Manager = omar.Id, Oil = oil.Id, Tea = tea.Id
        };
    }
}
