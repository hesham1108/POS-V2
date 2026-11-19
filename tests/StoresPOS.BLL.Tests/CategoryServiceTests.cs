using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Tests;

public class CategoryServiceTests
{
    [Fact]
    public async Task Create_And_BuildTree_WithParentChild()
    {
        await using var db = CreateDb();
        var service = new CategoryService(db);

        var root = await service.CreateAsync(new CategoryUpsertDto
        {
            NameAr = "مشروبات",
            NameEn = "Drinks"
        });

        var child = await service.CreateAsync(new CategoryUpsertDto
        {
            NameAr = "عصائر",
            NameEn = "Juices",
            ParentCategoryId = root.Id
        });

        var tree = await service.GetTreeAsync();
        Assert.Single(tree);
        Assert.Equal(root.Id, tree[0].Id);
        Assert.Single(tree[0].Children);
        Assert.Equal(child.Id, tree[0].Children[0].Id);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Throws()
    {
        await using var db = CreateDb();
        var service = new CategoryService(db);

        var root = await service.CreateAsync(new CategoryUpsertDto { NameAr = "أ", NameEn = "A" });
        var child = await service.CreateAsync(new CategoryUpsertDto
        {
            NameAr = "ب",
            NameEn = "B",
            ParentCategoryId = root.Id
        });

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.MoveAsync(root.Id, child.Id));
    }

    [Fact]
    public async Task Reorder_UpdatesSortOrder()
    {
        await using var db = CreateDb();
        var service = new CategoryService(db);

        var a = await service.CreateAsync(new CategoryUpsertDto { NameAr = "أ", NameEn = "A" });
        var b = await service.CreateAsync(new CategoryUpsertDto { NameAr = "ب", NameEn = "B" });

        await service.ReorderAsync(
        [
            new CategoryReorderItemDto { Id = a.Id, SortOrder = 5 },
            new CategoryReorderItemDto { Id = b.Id, SortOrder = 1 }
        ]);

        var list = await service.GetAllAsync();
        Assert.Equal(1, list.First(x => x.Id == b.Id).SortOrder);
        Assert.Equal(5, list.First(x => x.Id == a.Id).SortOrder);
    }

    [Fact]
    public async Task Deactivate_WithActiveChild_Throws()
    {
        await using var db = CreateDb();
        var service = new CategoryService(db);

        var root = await service.CreateAsync(new CategoryUpsertDto { NameAr = "أ", NameEn = "A" });
        await service.CreateAsync(new CategoryUpsertDto
        {
            NameAr = "ب",
            NameEn = "B",
            ParentCategoryId = root.Id
        });

        await Assert.ThrowsAsync<BusinessException>(() => service.DeactivateAsync(root.Id));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
