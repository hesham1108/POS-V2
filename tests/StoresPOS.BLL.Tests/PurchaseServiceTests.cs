using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class PurchaseServiceTests
{
    [Fact]
    public async Task Confirm_IncreasesStock_AndRecalculatesPricesFromMarkups()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId, treasuryId) = await SeedAsync(db);
        var purchases = CreatePurchases(db);

        var draft = await purchases.CreateDraftAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto
                {
                    ProductId = productId,
                    Quantity = 10,
                    UnitCost = 65m
                }
            ]
        }, userId);

        Assert.Equal(PurchaseInvoiceStatus.Draft, draft.Status);

        var confirmed = await purchases.ConfirmAsync(
            draft.Id, userId, treasuryId, await TestFactories.DefaultWarehouseIdAsync(db));
        Assert.Equal(PurchaseInvoiceStatus.Confirmed, confirmed.Status);
        Assert.Equal(650m, confirmed.SubTotal);
        Assert.Equal(treasuryId, confirmed.PaidFromTreasuryId);

        var product = await db.Products.SingleAsync();
        Assert.Equal(10m, product.StockQuantity);
        Assert.Equal(65m, product.PurchasePrice);
        Assert.Equal(78m, product.RetailPrice);       // 20% markup
        Assert.Equal(71.50m, product.WholesalePrice); // 10% markup

        var tx = await db.TreasuryTransactions.SingleAsync();
        Assert.Equal(TreasuryTransactionDirection.Out, tx.Direction);
        Assert.Equal(650m, tx.Amount);
        Assert.Equal(TreasuryTransactionSourceType.Purchase, tx.SourceType);
    }

    [Fact]
    public async Task CreateAndConfirm_PreservesLineSnapshots()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId, treasuryId) = await SeedAsync(db);
        var purchases = CreatePurchases(db);

        var invoice = await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto
                {
                    ProductId = productId,
                    Quantity = 2,
                    UnitCost = 40m
                }
            ]
        }, userId, treasuryId, await TestFactories.DefaultWarehouseIdAsync(db));

        Assert.Single(invoice.Items);
        Assert.Equal("Oil", invoice.Items[0].ProductNameEn);
        Assert.Equal("زيت", invoice.Items[0].ProductNameAr);
        Assert.Equal(40m, invoice.Items[0].UnitCost);

        // Catalog rename later must not rewrite history
        var product = await db.Products.SingleAsync();
        product.NameEn = "Changed";
        await db.SaveChangesAsync();

        var reloaded = await purchases.GetByIdAsync(invoice.Id);
        Assert.Equal("Oil", reloaded!.Items[0].ProductNameEn);
    }

    [Fact]
    public async Task UpdateConfirmed_Throws()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId, treasuryId) = await SeedAsync(db);
        var purchases = CreatePurchases(db);

        var invoice = await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 1, UnitCost = 10m }
            ]
        }, userId, treasuryId, await TestFactories.DefaultWarehouseIdAsync(db));

        await Assert.ThrowsAsync<BusinessException>(() => purchases.UpdateDraftAsync(invoice.Id, new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 2, UnitCost = 10m }
            ]
        }));
    }

    [Fact]
    public async Task Validate_RejectsZeroQuantity()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId, _) = await SeedAsync(db);
        var purchases = CreatePurchases(db);

        await Assert.ThrowsAsync<BusinessException>(() => purchases.CreateDraftAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 0, UnitCost = 10m }
            ]
        }, userId));
    }

    private static PurchaseService CreatePurchases(ApplicationDbContext db) =>
        TestFactories.Purchases(db);

    private static async Task<(int UserId, int SupplierId, int ProductId, int TreasuryId)> SeedAsync(ApplicationDbContext db)
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
            StockQuantity = 0,
            IsActive = true
        });
        db.Treasuries.Add(new Treasury
        {
            NameEn = "Main cash",
            NameAr = "الخزنة",
            Type = TreasuryType.Cash,
            OpeningBalance = 100_000m,
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        return (db.Users.Single().Id, db.Suppliers.Single().Id, db.Products.Single().Id, db.Treasuries.Single().Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
