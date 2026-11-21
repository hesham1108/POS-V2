using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

/// <summary>Phase 20: server-side paging, whitelisted sort, search.</summary>
public class PagingTests
{
    private sealed record Row(string Name, decimal Amount, int Hidden);

    private static readonly Row[] Rows =
    [
        new("c", 30m, 1), new("a", 10m, 2), new("e", 50m, 3), new("b", 20m, 4), new("d", 40m, 5)
    ];

    [Fact]
    public async Task Page_SortsByAllowedColumn_AndReportsTotals()
    {
        var page = await Rows.ToPagedAsync(new ListQuery { Page = 1, PageSize = 2, Sort = "amount", Dir = "desc" },
            ["Name", "Amount"], x => x.Amount, allowAnyPageSize: true);

        Assert.Equal(["e", "d"], page.Items.Select(x => x.Name));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(150m, page.Sum);
        Assert.Equal("Amount", page.Sort);
        Assert.True(page.Descending);
    }

    [Fact]
    public async Task Page_IgnoresColumnsNotOnTheList_AndClampsThePage()
    {
        var page = await Rows.ToPagedAsync(new ListQuery { Page = 99, PageSize = 2, Sort = "Hidden" },
            ["Name"], allowAnyPageSize: true);

        Assert.Null(page.Sort);                   // not sortable → original order kept
        Assert.Equal(3, page.Page);               // last page
        Assert.Equal(["d"], page.Items.Select(x => x.Name));
    }

    [Fact]
    public async Task Page_OnlyAllowsTheOfferedPageSizes()
    {
        var page = await Rows.ToPagedAsync(new ListQuery { PageSize = 3 }, ["Name"]);
        Assert.Equal(ListQuery.PageSizes[0], page.PageSize);

        var export = new ListQuery { PageSize = 3, Sort = "Name" }.ForExport();
        Assert.Equal(ListQuery.MaxExportRows, export.PageSize);
        Assert.Equal("Name", export.Sort);
    }

    [Fact]
    public async Task Products_50kRows_ReturnsOnePage_SortedAndSearchedOnTheServer()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new ApplicationDbContext(options);
        db.Products.AddRange(Enumerable.Range(1, 50_000).Select(i => new Product
        {
            NameEn = $"Item {i:D5}",
            NameAr = $"صنف {i:D5}",
            Barcode = $"62{i:D8}",
            Unit = "pcs",
            PurchasePrice = i % 100,
            RetailPrice = i % 100 + 1,
            WholesalePrice = i % 100,
            StockQuantity = i % 7,
            IsActive = true
        }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new ProductService(db);
        var page = await svc.GetPageAsync(new ProductListFilterDto(), new ListQuery { Page = 3, PageSize = 25, Sort = "NameEn", Dir = "desc" });

        Assert.Equal(50_000, page.TotalCount);
        Assert.Equal(2_000, page.TotalPages);
        Assert.Equal(25, page.Items.Count);
        Assert.Equal("Item 49950", page.Items[0].NameEn);  // rows 51–75 of the descending list

        var search = await svc.GetPageAsync(new ProductListFilterDto { Search = "Item 0012" }, new ListQuery());
        Assert.Equal(10, search.TotalCount);             // Item 00120 … Item 00129
    }
}
