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
        var (userId, supplierId, productId) = await SeedAsync(db);
        var purchases = new PurchaseService(db, new InventoryService(db));

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

        var confirmed = await purchases.ConfirmAsync(draft.Id, userId);
        Assert.Equal(PurchaseInvoiceStatus.Confirmed, confirmed.Status);
        Assert.Equal(650m, confirmed.SubTotal);

        var product = await db.Products.SingleAsync();
        Assert.Equal(10m, product.StockQuantity);
        Assert.Equal(65m, product.PurchasePrice);
        Assert.Equal(78m, product.RetailPrice);       // 20% markup
        Assert.Equal(71.50m, product.WholesalePrice); // 10% markup
    }

    [Fact]
    public async Task CreateAndConfirm_PreservesLineSnapshots()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId) = await SeedAsync(db);
        var purchases = new PurchaseService(db, new InventoryService(db));

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
        }, userId);

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
        var (userId, supplierId, productId) = await SeedAsync(db);
        var purchases = new PurchaseService(db, new InventoryService(db));

        var invoice = await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 1, UnitCost = 10m }
            ]
        }, userId);

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
        var (userId, supplierId, productId) = await SeedAsync(db);
        var purchases = new PurchaseService(db, new InventoryService(db));

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

    private static async Task<(int UserId, int SupplierId, int ProductId)> SeedAsync(ApplicationDbContext db)
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
        await db.SaveChangesAsync();

        return (db.Users.Single().Id, db.Suppliers.Single().Id, db.Products.Single().Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
