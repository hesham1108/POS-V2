using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class SupplierServiceTests
{
    [Fact]
    public async Task Create_And_List_ReturnsSupplier()
    {
        await using var db = CreateDb();
        var service = new SupplierService(db);

        var created = await service.CreateAsync(new SupplierUpsertDto
        {
            NameAr = "مورد",
            NameEn = "Vendor Co",
            Phone = "0100"
        });

        var list = await service.GetListAsync();
        Assert.Single(list);
        Assert.Equal(created.Id, list[0].Id);
        Assert.Equal(0, list[0].ConfirmedPurchaseCount);
    }

    [Fact]
    public async Task Totals_ReflectConfirmedPurchasesOnly()
    {
        await using var db = CreateDb();
        var (userId, supplierId, productId) = await SeedAsync(db);
        var purchases = new PurchaseService(db);
        var suppliers = new SupplierService(db);

        await purchases.CreateDraftAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 2, UnitCost = 10m }
            ]
        }, userId);

        await purchases.CreateAndConfirmAsync(new PurchaseInvoiceUpsertDto
        {
            SupplierId = supplierId,
            InvoiceDate = DateTime.UtcNow.Date,
            Items =
            [
                new PurchaseInvoiceItemInputDto { ProductId = productId, Quantity = 3, UnitCost = 20m }
            ]
        }, userId);

        var supplier = await suppliers.GetByIdAsync(supplierId);
        Assert.NotNull(supplier);
        Assert.Equal(1, supplier!.ConfirmedPurchaseCount);
        Assert.Equal(60m, supplier.TotalPurchasesAmount);
    }

    private static async Task<(int UserId, int SupplierId, int ProductId)> SeedAsync(ApplicationDbContext db)
    {
        db.Roles.Add(new Role { Name = "Admin", Description = "Admin" });
        await db.SaveChangesAsync();
        var roleId = db.Roles.Single().Id;

        db.Users.Add(new User
        {
            Username = "admin",
            DisplayName = "Admin",
            PasswordHash = "h",
            PasswordSalt = "s",
            RoleId = roleId,
            IsActive = true
        });
        db.Suppliers.Add(new Supplier { NameAr = "م", NameEn = "S", IsActive = true });
        db.Products.Add(new Product
        {
            NameAr = "صنف",
            NameEn = "Item",
            Unit = "pcs",
            PurchasePrice = 10m,
            RetailMarkupPercent = 20m,
            RetailPrice = 12m,
            WholesaleMarkupPercent = 10m,
            WholesalePrice = 11m,
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
