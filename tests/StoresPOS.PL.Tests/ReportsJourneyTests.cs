using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.BLL.Constants;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 23: report shell, Excel/CSV export with the screen's numbers, new reports, Profit.View.</summary>
public class ReportsJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ReportsJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> OwnerAsync()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");
        return admin;
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Profit")]
    [InlineData("Hours")]
    [InlineData("Products")]
    [InlineData("Audit")]
    [InlineData("Tax")]
    [InlineData("Purchases")]
    [InlineData("Inventory")]
    [InlineData("Shifts")]
    [InlineData("Drawer")]
    [InlineData("CashOuts")]
    [InlineData("Cheques")]
    [InlineData("ChequeAging")]
    public async Task EveryReport_Opens_AndExportsExcelAndCsv(string report)
    {
        var owner = await OwnerAsync();
        var page = await GetAsync(owner, $"/Reports/{report}");
        Assert.Contains($"/Reports/{report}", page.Url);
        Assert.Contains("class=\"report-tabs\"", page.Html);
        Assert.Contains("export=xlsx", page.Html);
        Assert.Contains("/js/reports.js", page.Html);
        AssertAssetUrlsResolved(page.Html);

        var xlsx = await owner.GetAsync($"/Reports/{report}?export=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);
        using (var wb = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync()))
        {
            Assert.NotEmpty(wb.Worksheets);
        }

        var csv = await owner.GetAsync($"/Reports/{report}?export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SalesExcel_HasTheSameNetSalesAsTheScreen()
    {
        var owner = await OwnerAsync();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A completed sale today, written straight to the database.
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(x => x.Username == "admin");
            var shift = new Shift { OpenedByUserId = user.Id, OpenedAtUtc = DateTime.UtcNow, Status = ShiftStatus.Open };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            var oil = await db.Products.FirstAsync(x => x.Barcode == "622100");
            db.SalesInvoices.Add(new SalesInvoice
            {
                InvoiceNumber = "S-900001", InvoiceSequence = 900001, Status = SalesInvoiceStatus.Completed,
                ShiftId = shift.Id, CashierUserId = user.Id, Subtotal = 156m, Total = 156m,
                PaymentMethod = PaymentMethod.Cash, CompletedAtUtc = DateTime.UtcNow,
                Items = [new SalesInvoiceItem { ProductId = oil.Id, ProductNameEn = oil.NameEn, ProductNameAr = oil.NameAr, Unit = oil.Unit, Quantity = 2, CatalogUnitPrice = 78m, UnitPrice = 78m, LineTotal = 156m, UnitCost = 65m }]
            });
            await db.SaveChangesAsync();
        }

        var page = await GetAsync(owner, $"/Reports/Sales?from={today}&to={today}");
        Assert.Contains("156.00", page.Html);
        Assert.Contains("Gross profit", page.Html);                     // owner has Profit.View
        Assert.Contains("productId=", page.Html);                       // drill-down to the invoices

        var xlsx = await owner.GetAsync($"/Reports/Sales?from={today}&to={today}&export=xlsx");
        using var wb = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync());
        var sheet = wb.Worksheets.First();
        var netRow = sheet.RowsUsed().First(r => r.Cell(1).GetString() == "Net sales");
        Assert.Equal(156m, netRow.Cell(2).GetValue<decimal>());

        var drill = await GetAsync(owner, $"/Sales?cashierId=1&from={today}&to={today}");
        Assert.Contains("S-900001", drill.Html);
    }

    [Fact]
    public async Task Profit_IsHiddenFromPeopleWithoutProfitView()
    {
        await _factory.ResetShiftsAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mona = await db.Users.SingleAsync(x => x.Username == "mona");
            db.UserPermissions.RemoveRange(db.UserPermissions.Where(x => x.UserId == mona.Id));
            db.UserPermissions.Add(new UserPermission { UserId = mona.Id, Permission = AppPermissions.ReportsView });
            db.UserPermissions.Add(new UserPermission { UserId = mona.Id, Permission = AppPermissions.PosUse });
            mona.PermissionStamp++;
            await db.SaveChangesAsync();
        }

        var mona2 = _factory.CreateBrowser();
        mona2.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(mona2, "mona", CashierPassword);
        var sales = await GetAsync(mona2, "/Reports/Sales");
        Assert.Contains("/Reports/Sales", sales.Url);
        Assert.DoesNotContain("Gross profit", sales.Html);
        Assert.DoesNotContain("/Reports/Profit\"", sales.Html);
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona2, "/Reports/Profit")).Url);

        var xlsx = await mona2.GetAsync("/Reports/Sales?export=xlsx");
        using var wb = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(wb.Worksheets.First().CellsUsed(), c => c.GetString() == "Gross profit");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mona = await db.Users.SingleAsync(x => x.Username == "mona");
            db.UserPermissions.RemoveRange(db.UserPermissions.Where(x => x.UserId == mona.Id));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Reports_InArabic_AreTranslated()
    {
        await _factory.ResetShiftsAsync();
        var ar = _factory.CreateBrowser();
        ar.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        await LoginAsync(ar, "admin", "Admin@12345");
        foreach (var report in new[] { "Hours", "Audit", "Tax", "Drawer", "ChequeAging", "Products", "Profit" })
        {
            var page = await GetAsync(ar, $"/Reports/{report}");
            Assert.Contains("dir=\"rtl\"", page.Html);
            Assert.DoesNotMatch(new Regex(">(Busy hours|Drawer differences|Cheque aging|Product performance|Discounts and price changes)<"), page.Html);
        }
    }
}
