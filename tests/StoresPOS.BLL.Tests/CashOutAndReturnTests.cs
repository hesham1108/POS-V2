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

/// <summary>Phase 9: cash-outs (journey E) and returns (journey G) and how they move the drawer.</summary>
public class CashOutAndReturnTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Ctx : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required ShiftService Shifts { get; init; }
        public required SalesService Sales { get; init; }
        public required CashOutService CashOuts { get; init; }
        public required ReturnService Returns { get; init; }
        public required Clock Clock { get; init; }
        public int Cashier { get; init; }
        public int Manager { get; init; }
        public int Oil { get; init; }
        public int Tea { get; init; }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    // ------------------------------------------------------------ cash-outs

    [Fact]
    public async Task CashOut_ReducesExpectedCash_AndIsListedPerUser()
    {
        await using var c = await CreateAsync();
        var result = await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 30m, Reason = " Cleaning supplies " }, c.Cashier);

        Assert.Equal(70m, result.ExpectedCash);          // 100 opening − 30
        Assert.False(result.ExceededDrawer);

        var summary = (await c.Shifts.GetOpenShiftAsync())!;
        Assert.Equal(30m, summary.CashOutsTotal);
        Assert.Equal(1, summary.CashOutsCount);
        Assert.Equal(70m, summary.ExpectedCash);
        Assert.Equal(30m, summary.Participants.Single(p => p.UserId == c.Cashier).CashOuts);

        var list = await c.CashOuts.GetListAsync(new CashOutFilterDto { ShiftId = summary.Id });
        Assert.Equal("Cleaning supplies", Assert.Single(list).Reason);
    }

    [Fact]
    public async Task CashOut_AboveDrawer_IsAllowed_WithWarning()
    {
        await using var c = await CreateAsync();
        var result = await c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 150m, Reason = "Supplier" }, c.Cashier);
        Assert.True(result.ExceededDrawer);
        Assert.Equal(-50m, result.ExpectedCash);
    }

    [Theory]
    [InlineData(0, "x", CashOutService.MsgInvalidAmount)]
    [InlineData(-5, "x", CashOutService.MsgInvalidAmount)]
    [InlineData(10, "  ", CashOutService.MsgReasonRequired)]
    public async Task CashOut_Validates(decimal amount, string reason, string expected)
    {
        await using var c = await CreateAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = amount, Reason = reason }, c.Cashier));
        Assert.Equal(expected, ex.MessageKey);
    }

    [Fact]
    public async Task CashOut_NeedsShift()
    {
        await using var c = await CreateAsync(openShift: false);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => c.CashOuts.CreateAsync(new CreateCashOutDto { Amount = 10m, Reason = "x" }, c.Cashier));
        Assert.Equal(CashOutService.MsgNotInShift, ex.MessageKey);
    }

    // ------------------------------------------------------------ returns

    [Fact]
    public async Task PartialCashReturn_Restocks_RefundsDrawer_AndTracksAvailable()
    {
        await using var c = await CreateAsync();
        var sale = await SellAsync(c, (c.Oil, 3m), (c.Tea, 1m));   // 3×78 + 62 = 296 cash
        var invoice = (await c.Returns.GetReturnableAsync(sale))!;
        var oilLine = invoice.Lines.Single(x => x.ProductId == c.Oil);

        var ret = await c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = oilLine.SalesInvoiceItemId, Quantity = 2m }],
            RefundMethod = PaymentMethod.Cash,
            Reason = "Damaged"
        }, c.Manager);

        Assert.Equal("R-000001", ret.ReturnNumber);
        Assert.Equal(156m, ret.TotalAmount);

        var oil = await c.Db.Products.AsNoTracking().SingleAsync(x => x.Id == c.Oil);
        Assert.Equal(9m, oil.StockQuantity);   // 10 − 3 + 2
        Assert.True(await c.Db.StockMovements.AnyAsync(x =>
            x.MovementType == StockMovementType.SaleReturn && x.ReferenceId == ret.Id && x.QuantityChange == 2m));

        var summary = (await c.Shifts.GetOpenShiftAsync())!;
        Assert.Equal(156m, summary.CashRefunds);
        Assert.Equal(100m + 296m - 156m, summary.ExpectedCash);

        invoice = (await c.Returns.GetReturnableAsync(sale))!;
        Assert.Equal(1m, invoice.Lines.Single(x => x.ProductId == c.Oil).AvailableQuantity);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = oilLine.SalesInvoiceItemId, Quantity = 2m }],
            Reason = "Again"
        }, c.Manager));
        Assert.Equal(ReturnService.MsgTooMany, ex.MessageKey);

        var listed = Assert.Single(await c.Sales.GetInvoicesAsync());
        Assert.True(listed.IsPartlyReturned);
    }

    [Fact]
    public async Task Discount_IsShared_AndFullReturnRefundsExactTotal()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Manager, c.Oil, 1m);   // 78
        await c.Sales.AddProductAsync(c.Manager, c.Tea, 2m);   // 124
        await c.Sales.SetDiscountAsync(c.Manager, new DiscountDto { Amount = 20.33m });
        var sale = (await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 500m })).InvoiceId;
        var invoice = (await c.Returns.GetReturnableAsync(sale))!;
        Assert.Equal(181.67m, invoice.Total);

        var tea = invoice.Lines.Single(x => x.ProductId == c.Tea);
        var first = await c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = tea.SalesInvoiceItemId, Quantity = 1m }],
            Reason = "x"
        }, c.Manager);
        Assert.Equal(55.76m, first.TotalAmount);   // 62 × 181.67/202

        var rest = invoice.Lines.Select(l => new ReturnLineInputDto
        {
            SalesInvoiceItemId = l.SalesInvoiceItemId,
            Quantity = l.ProductId == c.Tea ? 1m : l.SoldQuantity
        }).ToList();
        var second = await c.Returns.CreateAsync(new CreateReturnDto { InvoiceId = sale, Lines = rest, Reason = "x" }, c.Manager);

        Assert.Equal(181.67m, first.TotalAmount + second.TotalAmount);
        Assert.True((await c.Returns.GetReturnableAsync(sale))!.IsFullyReturned);
        Assert.True(Assert.Single(await c.Sales.GetInvoicesAsync()).IsFullyReturned);
    }

    [Fact]
    public async Task NonCashRefund_MustMatchOriginalMethod_AndSkipsDrawer()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Manager, c.Oil, 1m);
        var sale = (await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.Card })).InvoiceId;
        var line = (await c.Returns.GetReturnableAsync(sale))!.Lines[0];
        CreateReturnDto Dto(PaymentMethod m) => new()
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = line.SalesInvoiceItemId, Quantity = 1m }],
            RefundMethod = m,
            Reason = "x"
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(Dto(PaymentMethod.InstaPay), c.Manager));
        Assert.Equal(ReturnService.MsgInvalidMethod, ex.MessageKey);

        await c.Returns.CreateAsync(Dto(PaymentMethod.Card), c.Manager);
        var summary = (await c.Shifts.GetOpenShiftAsync())!;
        Assert.Equal(0m, summary.CashRefunds);
        Assert.Equal(78m, summary.NonCashRefunds);
        Assert.Equal(100m, summary.ExpectedCash);
    }

    [Fact]
    public async Task CashRefund_NeedsOpenShift_NonCashDoesNot()
    {
        await using var c = await CreateAsync();
        await c.Sales.AddProductAsync(c.Manager, c.Oil, 2m);
        var sale = (await c.Sales.CompleteAsync(c.Manager, new PaymentDto { Method = PaymentMethod.InstaPay })).InvoiceId;
        var shiftId = (await c.Shifts.GetOpenShiftAsync())!.Id;
        await c.Shifts.CloseAsync(new CloseShiftDto { ShiftId = shiftId, HandoverTreasuryId = 1 }, c.Manager);
        var line = (await c.Returns.GetReturnableAsync(sale))!.Lines[0];

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = line.SalesInvoiceItemId, Quantity = 1m }],
            RefundMethod = PaymentMethod.Cash,
            Reason = "x"
        }, c.Manager));
        Assert.Equal(ReturnService.MsgNoShiftForCash, ex.MessageKey);

        var ret = await c.Returns.CreateAsync(new CreateReturnDto
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = line.SalesInvoiceItemId, Quantity = 1m }],
            RefundMethod = PaymentMethod.InstaPay,
            Reason = "x"
        }, c.Manager);
        Assert.Null(ret.ShiftId);
    }

    [Fact]
    public async Task Return_Blocked_WithoutPermission_AfterWindow_AndWithoutReason()
    {
        await using var c = await CreateAsync();
        var sale = await SellAsync(c, (c.Oil, 1m));
        var line = (await c.Returns.GetReturnableAsync(sale))!.Lines[0];
        CreateReturnDto Dto(string? reason) => new()
        {
            InvoiceId = sale,
            Lines = [new ReturnLineInputDto { SalesInvoiceItemId = line.SalesInvoiceItemId, Quantity = 1m }],
            Reason = reason
        };

        // Cashier "Returns" cell is optional and starts OFF.
        var denied = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(Dto("x"), c.Cashier));
        Assert.Equal(ReturnService.MsgNoPermission, denied.MessageKey);

        var noReason = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(Dto(" "), c.Manager));
        Assert.Equal(ReturnService.MsgReasonRequired, noReason.MessageKey);

        c.Clock.Now = c.Clock.Now.AddDays(15);
        Assert.False((await c.Returns.GetReturnableAsync(sale))!.IsWithinWindow);
        var late = await Assert.ThrowsAsync<BusinessException>(() => c.Returns.CreateAsync(Dto("x"), c.Manager));
        Assert.Equal("This invoice is older than 14 days and can no longer be returned.", late.Message);
    }

    [Fact]
    public async Task FindInvoice_ByScannedNumber_OrBareSequence()
    {
        await using var c = await CreateAsync();
        var sale = await SellAsync(c, (c.Oil, 1m));
        Assert.Equal(sale, await c.Returns.FindInvoiceIdAsync("s-000001"));
        Assert.Equal(sale, await c.Returns.FindInvoiceIdAsync("1"));
        Assert.Null(await c.Returns.FindInvoiceIdAsync("S-000099"));
    }

    // ------------------------------------------------------------ fixture

    private static async Task<int> SellAsync(Ctx c, params (int ProductId, decimal Qty)[] lines)
    {
        foreach (var (productId, qty) in lines)
        {
            await c.Sales.AddProductAsync(c.Cashier, productId, qty);
        }

        return (await c.Sales.CompleteAsync(c.Cashier, new PaymentDto { Method = PaymentMethod.Cash, AmountPaid = 1000m })).InvoiceId;
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
            StockQuantity = 5m, LowStockThreshold = 1m
        };
        db.Products.AddRange(oil, tea);
        await db.SaveChangesAsync();

        var clock = new Clock(DateTimeOffset.UtcNow);
        var inventory = new InventoryService(db);
        var permissions = new PermissionService(db);
        var paymentMethods = new PaymentMethodService(db);
        await paymentMethods.EnsureSeededAsync();
        var treasuries = new TreasuryService(db, clock);
        await treasuries.EnsureSeededAsync();
        var shifts = new ShiftService(db, treasuries, clock);
        var sales = new SalesService(db, shifts, inventory, permissions, paymentMethods, treasuries, new ChequeService(db, treasuries, clock), clock);

        if (openShift)
        {
            await shifts.OpenAsync(100m, mona.Id);
            await shifts.ResumeAsync(omar.Id);
        }

        return new Ctx
        {
            Db = db,
            Shifts = shifts,
            Sales = sales,
            CashOuts = new CashOutService(db, shifts, clock),
            Returns = new ReturnService(db, inventory, permissions, paymentMethods, Options.Create(new StoreSettings()), clock),
            Clock = clock,
            Cashier = mona.Id,
            Manager = omar.Id,
            Oil = oil.Id,
            Tea = tea.Id
        };
    }
}
