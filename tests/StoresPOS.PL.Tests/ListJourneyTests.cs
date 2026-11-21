using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 20: list pages share paging, server sort, search (q) and CSV export.</summary>
public class ListJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ListJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AdminAsync()
    {
        await _factory.ResetShiftsAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Customers.AnyAsync(x => x.Name.StartsWith("Paging ")))
            {
                db.Customers.AddRange(Enumerable.Range(1, 60).Select(i => new Customer
                {
                    Name = $"Paging {i:D2}",
                    Phone = $"0100000{i:D4}",
                    IsActive = true
                }));
                await db.SaveChangesAsync();
            }
        }

        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");
        return admin;
    }

    [Fact]
    public async Task Customers_PageSortAndSearch_RunOnTheServer()
    {
        var admin = await AdminAsync();

        var first = await GetAsync(admin, "/Customers?q=Paging&sort=Name&dir=asc");
        Assert.Contains("Paging 01", first.Html);
        Assert.Contains("Paging 25", first.Html);
        Assert.DoesNotContain("Paging 26", first.Html);
        Assert.Contains("1–25 of 60", first.Html);
        Assert.Contains("class=\"sp-pager\"", first.Html);
        Assert.Contains("aria-sort=\"ascending\"", first.Html);

        var third = await GetAsync(admin, "/Customers?q=Paging&sort=Name&dir=desc&page=3");
        Assert.Contains("Paging 10", third.Html);
        Assert.DoesNotContain("Paging 11", third.Html);
        Assert.Contains("51–60 of 60", third.Html);

        var search = await GetAsync(admin, "/Customers?q=Paging%2005");
        Assert.Contains("Paging 05", search.Html);
        Assert.DoesNotContain("Paging 06", search.Html);
        Assert.Contains("Search: Paging 05", search.Html);   // removable chip

        var big = await GetAsync(admin, "/Customers?q=Paging&pageSize=100");
        Assert.Contains("Paging 60", big.Html);
    }

    [Fact]
    public async Task Customers_ExportCsv_HasEveryMatchingRow()
    {
        var admin = await AdminAsync();
        var response = await admin.GetAsync("/Customers?q=Paging&export=csv&page=2");
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Equal(61, csv.Trim().Split('\n').Length);      // header + 60 rows, not one page
        Assert.Contains("Paging 60", csv);
    }

    [Theory]
    [InlineData("/Sales")]
    [InlineData("/Products")]
    [InlineData("/Inventory/Movements")]
    [InlineData("/Customers")]
    [InlineData("/Suppliers")]
    [InlineData("/Purchases")]
    [InlineData("/Returns")]
    [InlineData("/CashOuts")]
    [InlineData("/Cheques")]
    public async Task EveryList_HasToolbarSortAndCsv(string url)
    {
        var admin = await AdminAsync();
        var page = await GetAsync(admin, url + "?sort=bogus&page=-5&pageSize=7");
        Assert.Contains("class=\"sp-list-toolbar\"", page.Html);
        Assert.Contains("export=csv", page.Html);
        Assert.True(page.Html.Contains("sp-th-sort") || page.Html.Contains("class=\"sp-empty\""), "sortable table or empty state expected");

        var csv = await admin.GetAsync(url + "?export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Views_UseOnlyTheSharedTable()
    {
        var views = Path.Combine(RepoRoot(), "src", "StoresPOS.PL", "Views");
        var offenders = Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "<table class=\"([^\"]*)\"")
                .Where(m => m.Groups[1].Value.Split(' ').Contains("table") && !m.Groups[1].Value.Split(' ').Contains("sp-table"))
                .Select(m => Path.GetRelativePath(views, f) + ": " + m.Value))
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StoresPOS.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
