using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class WarehouseServiceTests
{
    [Fact]
    public async Task EnsureSeeded_MigratesProductStockIntoMainWarehouse()
    {
        await using var db = CreateDb();
        var (userId, productId) = await SeedAsync(db, startingStock: 12m);
        var warehouses = TestFactories.Warehouses(db);

        await warehouses.EnsureSeededAsync();
        var mainId = await warehouses.GetSellingWarehouseIdAsync();
        var stock = await db.WarehouseStocks.SingleAsync(x => x.WarehouseId == mainId && x.ProductId == productId);
        Assert.Equal(12m, stock.Quantity);
        Assert.Equal(userId, db.Users.Single().Id);
    }

    [Fact]
    public async Task Transfer_MovesStockBetweenWarehouses_WithoutChangingProductTotal()
    {
        await using var db = CreateDb();
        var (userId, productId) = await SeedAsync(db, startingStock: 10m);
        var warehouses = TestFactories.Warehouses(db);
        await warehouses.EnsureSeededAsync();
        var fromId = await warehouses.GetSellingWarehouseIdAsync();

        var secondary = await warehouses.CreateAsync(new WarehouseFormDto
        {
            NameEn = "Back room",
            NameAr = "المخزن الخلفي",
            IsActive = true
        });

        var transfer = await warehouses.CreateTransferAsync(new StockTransferCreateDto
        {
            FromWarehouseId = fromId,
            ToWarehouseId = secondary.Id,
            Notes = "Restock shelf",
            Items = [new StockTransferLineDto { ProductId = productId, Quantity = 4m }]
        }, userId);

        Assert.StartsWith("T-", transfer.TransferNumber);
        Assert.Equal(10m, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(6m, await db.WarehouseStocks.Where(x => x.WarehouseId == fromId && x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(4m, await db.WarehouseStocks.Where(x => x.WarehouseId == secondary.Id && x.ProductId == productId).Select(x => x.Quantity).SingleAsync());

        var movements = await db.StockMovements.Where(x => x.ReferenceType == InventoryService.TransferReferenceType).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(StockMovementType.Transfer, m.MovementType));
    }

    [Fact]
    public async Task Transfer_InsufficientStock_Throws()
    {
        await using var db = CreateDb();
        var (userId, productId) = await SeedAsync(db, startingStock: 2m);
        var warehouses = TestFactories.Warehouses(db);
        await warehouses.EnsureSeededAsync();
        var fromId = await warehouses.GetSellingWarehouseIdAsync();
        var secondary = await warehouses.CreateAsync(new WarehouseFormDto
        {
            NameEn = "Back",
            NameAr = "خلفي",
            IsActive = true
        });

        await Assert.ThrowsAsync<BusinessException>(() => warehouses.CreateTransferAsync(new StockTransferCreateDto
        {
            FromWarehouseId = fromId,
            ToWarehouseId = secondary.Id,
            Items = [new StockTransferLineDto { ProductId = productId, Quantity = 5m }]
        }, userId));
    }

    [Fact]
    public async Task PurchaseIntoWarehouse_UpdatesThatWarehouseOnly()
    {
        await using var db = CreateDb();
        var (userId, productId) = await SeedAsync(db, startingStock: 0m);
        db.Suppliers.Add(new Supplier { NameAr = "مورد", NameEn = "Supplier", IsActive = true });
        db.Treasuries.Add(new Treasury
        {
            NameEn = "Main", NameAr = "رئيسية", Type = TreasuryType.Cash,
            OpeningBalance = 100_000m, IsActive = true, IsDefault = true
        });
        await db.SaveChangesAsync();

        var warehouses = TestFactories.Warehouses(db);
        await warehouses.EnsureSeededAsync();
        var secondary = await warehouses.CreateAsync(new WarehouseFormDto
        {
            NameEn = "Receiving",
            NameAr = "الاستلام",
            IsActive = true
        });

        var purchases = TestFactories.Purchases(db);
        await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = db.Suppliers.Single().Id,
            InvoiceDate = DateTime.UtcNow.Date,
            Items = [new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 7, UnitCost = 10m }]
        }, userId, db.Treasuries.Single().Id, secondary.Id);

        Assert.Equal(7m, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(7m, await db.WarehouseStocks.Where(x => x.WarehouseId == secondary.Id && x.ProductId == productId).Select(x => x.Quantity).SingleAsync());
        var mainId = await warehouses.GetSellingWarehouseIdAsync();
        Assert.Equal(0m, await db.WarehouseStocks.Where(x => x.WarehouseId == mainId && x.ProductId == productId).Select(x => (decimal?)x.Quantity).FirstOrDefaultAsync() ?? 0m);
    }

    private static async Task<(int UserId, int ProductId)> SeedAsync(
        ApplicationDbContext db,
        decimal startingStock = 0m)
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
            LowStockThreshold = 5m,
            IsActive = true
        });
        await db.SaveChangesAsync();
        return (db.Users.Single().Id, db.Products.Single().Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
