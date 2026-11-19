using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class InventoryServiceTests
{
    [Fact]
    public async Task PurchaseConfirm_WritesMovement_AndUpdatesBalance()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId) = await SeedAsync(db);
        var inventory = new InventoryService(db);
        var treasuryId = await SeedTreasuryAsync(db);
        var purchases = new PurchaseService(db, inventory, new TreasuryService(db, TimeProvider.System), new ChequeService(db, new TreasuryService(db, TimeProvider.System), TimeProvider.System));

        await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 8, UnitCost = 65m }
            ]
        }, userId, treasuryId);

        var balances = await inventory.GetBalancesAsync();
        Assert.Equal(8m, balances.Single().StockQuantity);

        var movements = await inventory.GetMovementsAsync(new StockMovementFilterDto { ProductId = productId });
        Assert.Single(movements);
        Assert.Equal(StockMovementType.Purchase, movements[0].MovementType);
        Assert.Equal(8m, movements[0].QuantityChange);
        Assert.Equal(0m, movements[0].QuantityBefore);
        Assert.Equal(8m, movements[0].QuantityAfter);
        Assert.Equal(InventoryService.PurchaseReferenceType, movements[0].ReferenceType);
    }

    [Fact]
    public async Task Adjust_ByDelta_TracksUserAndReason()
    {
        await using var db = CreateDb();
        var (userId, _, productId) = await SeedAsync(db, startingStock: 10m);
        var inventory = new InventoryService(db);

        var movement = await inventory.AdjustStockAsync(new StockAdjustmentDto
        {
            ProductId = productId,
            QuantityChange = -3m,
            Reason = "Damaged units"
        }, userId);

        Assert.Equal(StockMovementType.Adjustment, movement.MovementType);
        Assert.Equal(-3m, movement.QuantityChange);
        Assert.Equal(7m, movement.QuantityAfter);
        Assert.Equal("Damaged units", movement.Reason);
        Assert.Equal("admin", movement.CreatedByUsername);
    }

    [Fact]
    public async Task Adjust_SetCountedQuantity()
    {
        await using var db = CreateDb();
        var (userId, _, productId) = await SeedAsync(db, startingStock: 10m);
        var inventory = new InventoryService(db);

        var movement = await inventory.AdjustStockAsync(new StockAdjustmentDto
        {
            ProductId = productId,
            SetQuantityTo = 12m,
            Reason = "Cycle count"
        }, userId);

        Assert.Equal(2m, movement.QuantityChange);
        Assert.Equal(12m, movement.QuantityAfter);
    }

    [Fact]
    public async Task Adjust_BelowZero_Throws()
    {
        await using var db = CreateDb();
        var (userId, _, productId) = await SeedAsync(db, startingStock: 2m);
        var inventory = new InventoryService(db);

        await Assert.ThrowsAsync<BusinessException>(() => inventory.AdjustStockAsync(new StockAdjustmentDto
        {
            ProductId = productId,
            QuantityChange = -5m,
            Reason = "Too much"
        }, userId));
    }

    [Fact]
    public async Task LowStock_FiltersByThreshold()
    {
        await using var db = CreateDb();
        await SeedAsync(db, startingStock: 2m, threshold: 5m);
        var inventory = new InventoryService(db);

        var low = await inventory.GetLowStockAsync();
        Assert.Single(low);
        Assert.True(low[0].IsLowStock);
    }

    private static async Task<(int UserId, int SupplierId, int ProductId)> SeedAsync(
        ApplicationDbContext db,
        decimal startingStock = 0m,
        decimal threshold = 5m)
    {
        db.Roles.Add(new Role { Name = "Admin", Description = "Admin" });
        await db.SaveChangesAsync();

        db.Users.Add(new User
        {
            Username = "admin",
            DisplayName = "Admin",
            PasswordHash = "h",
            PasswordSalt = "s",
            RoleId = db.Roles.Single().Id,
            IsActive = true
        });
        db.Suppliers.Add(new Supplier { NameAr = "مورد", NameEn = "Supplier", IsActive = true });
        db.Products.Add(new Product
        {
            NameAr = "زيت",
            NameEn = "Oil",
            Unit = "L",
            PurchasePrice = 50m,
            RetailMarkupPercent = 20m,
            RetailPrice = 60m,
            WholesaleMarkupPercent = 10m,
            WholesalePrice = 55m,
            StockQuantity = startingStock,
            LowStockThreshold = threshold,
            IsActive = true
        });
        await db.SaveChangesAsync();

        return (db.Users.Single().Id, db.Suppliers.Single().Id, db.Products.Single().Id);
    }

    private static async Task<int> SeedTreasuryAsync(ApplicationDbContext db)
    {
        db.Treasuries.Add(new Treasury
        {
            NameEn = "Main",
            NameAr = "رئيسية",
            Type = TreasuryType.Cash,
            OpeningBalance = 100_000m,
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();
        return db.Treasuries.Single().Id;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
