using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task Create_ComputesRetailFromMarkup()
    {
        await using var db = CreateDb();
        var products = new ProductService(db);

        var created = await products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "زيت",
            NameEn = "Oil",
            Unit = "L",
            PurchasePrice = 65m,
            RetailMarkupPercent = 20m,
            WholesaleMarkupPercent = 10m,
            LowStockThreshold = 5m
        });

        Assert.Equal(78m, created.RetailPrice);
        Assert.Equal(20m, created.RetailMarkupPercent);
        Assert.Equal(71.50m, created.WholesalePrice);
        Assert.True(created.IsLowStock);
    }

    [Fact]
    public async Task Create_DuplicateBarcode_Throws()
    {
        await using var db = CreateDb();
        var products = new ProductService(db);

        await products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "أ",
            NameEn = "A",
            Barcode = "111",
            PurchasePrice = 10m,
            RetailPrice = 12m,
            WholesalePrice = 11m
        });

        await Assert.ThrowsAsync<BusinessException>(() => products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "ب",
            NameEn = "B",
            Barcode = "111",
            PurchasePrice = 10m,
            RetailPrice = 12m,
            WholesalePrice = 11m
        }));
    }

    [Fact]
    public async Task Update_FromSellPrice_RecalculatesMarkup()
    {
        await using var db = CreateDb();
        var products = new ProductService(db);

        var created = await products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "شاي",
            NameEn = "Tea",
            PurchasePrice = 50m,
            RetailPrice = 60m,
            WholesalePrice = 55m
        });

        var updated = await products.UpdateAsync(created.Id, new ProductUpsertDto
        {
            NameAr = "شاي",
            NameEn = "Tea",
            PurchasePrice = 50m,
            RetailPrice = 75m,
            WholesalePrice = 55m,
            IsActive = true
        });

        Assert.Equal(75m, updated.RetailPrice);
        Assert.Equal(50m, updated.RetailMarkupPercent);
    }

    [Fact]
    public async Task List_FiltersBySearchAndCategory()
    {
        await using var db = CreateDb();
        var categories = new CategoryService(db);
        var products = new ProductService(db);

        var cat = await categories.CreateAsync(new CategoryUpsertDto
        {
            NameAr = "طعام",
            NameEn = "Food"
        });

        await products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "جبنة",
            NameEn = "Cheese",
            Barcode = "C-1",
            CategoryId = cat.Id,
            PurchasePrice = 40m,
            RetailPrice = 50m,
            WholesalePrice = 45m
        });

        await products.CreateAsync(new ProductUpsertDto
        {
            NameAr = "ماء",
            NameEn = "Water",
            Barcode = "W-1",
            PurchasePrice = 5m,
            RetailPrice = 7m,
            WholesalePrice = 6m
        });

        var filtered = await products.GetListAsync(new ProductListFilterDto
        {
            Search = "Chee",
            CategoryId = cat.Id
        });

        Assert.Single(filtered);
        Assert.Equal("Cheese", filtered[0].NameEn);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
