using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 26: the cashier's till menu, number-only invoice search, the off-canvas menu and the appearance picker.</summary>
public class UiRefreshTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public UiRefreshTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cashier_SeesCashOutsAndShifts_OnThePosRail_AndCanOpenThem()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "" });

        Assert.Contains("/Pos", pos.Url);
        Assert.Contains("class=\"pos-rail__link\" href=\"/CashOuts\"", pos.Html);
        Assert.Contains("class=\"pos-rail__link\" href=\"/Shifts/Current\"", pos.Html);
        Assert.Contains("data-sidebar-toggle", pos.Html); // menu button for narrow screens

        // The cash-out list opens for the cashier (no bounce to the dashboard) and the Collect button's form is on the till.
        var list = await GetAsync(mona, "/CashOuts");
        Assert.Contains("/CashOuts", list.Url);
        Assert.Contains("id=\"cashout-modal\"", pos.Html);
    }

    [Fact]
    public async Task SalesAndReturns_SearchByTheBareInvoiceNumber()
    {
        await _factory.ResetShiftsAsync();
        int returnableInvoice;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var admin = await db.Users.FirstAsync(x => x.Username == "admin");
            var product = await db.Products.FirstAsync();
            var shift = new Shift { OpenedByUserId = admin.Id, OpenedAtUtc = DateTime.UtcNow.AddHours(-1), Status = ShiftStatus.Closed, ClosedAtUtc = DateTime.UtcNow, ClosedByUserId = admin.Id };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            foreach (var seq in new[] { 8801, 88010 })
            {
                db.SalesInvoices.Add(new SalesInvoice
                {
                    InvoiceNumber = $"S-{seq:D6}", InvoiceSequence = seq, Status = SalesInvoiceStatus.Completed, ShiftId = shift.Id, CashierUserId = admin.Id,
                    Subtotal = 10m, Total = 10m, PaymentMethod = PaymentMethod.Cash, AmountPaid = 10, CompletedAtUtc = DateTime.UtcNow,
                    Items = [new SalesInvoiceItem { ProductId = product.Id, ProductNameAr = product.NameAr, ProductNameEn = product.NameEn, Unit = product.Unit, Quantity = 1, CatalogUnitPrice = 10, UnitPrice = 10, LineTotal = 10, UnitCost = 5 }]
                });
            }

            await db.SaveChangesAsync();
            returnableInvoice = (await db.SalesInvoices.FirstAsync(x => x.InvoiceSequence == 8801)).Id;
        }

        var admin2 = _factory.CreateBrowser();
        await LoginAsync(admin2, "admin", "Admin@12345");

        // "8801" is that invoice's own number: it must not also bring up 88010.
        var found = (await GetAsync(admin2, "/Sales?q=8801")).Html;
        Assert.Contains("S-008801", found);
        Assert.DoesNotContain("S-088010", found);

        // Typing the zero-padded number works too, and the return screen finds it by the bare number.
        Assert.Contains("S-008801", (await GetAsync(admin2, "/Sales?q=008801")).Html);
        var ret = await GetAsync(admin2, "/Returns/Create?number=8801");
        Assert.Contains("S-008801", ret.Html);
        Assert.Equal(returnableInvoice > 0, true);
    }

    [Fact]
    public async Task Layout_HasAnOffCanvasMenu_AndTheAppearanceTabOffersThemeCardsAndPalettes()
    {
        await _factory.ResetShiftsAsync();
        var admin = _factory.CreateBrowser();
        await LoginAsync(admin, "admin", "Admin@12345");

        var home = (await GetAsync(admin, "/Products")).Html;
        Assert.Contains("id=\"app-sidebar\"", home);
        Assert.Contains("data-sidebar-toggle", home);
        Assert.Contains("/js/sidebar.js", home);

        var appearance = (await GetAsync(admin, "/Settings?tab=appearance")).Html;
        Assert.Contains("data-theme-choice", appearance);
        Assert.Contains("data-swatch", appearance);
        Assert.Contains("/js/appearance.js", appearance);
        Assert.Contains("name=\"Form.UiTheme\" value=\"dark\"", appearance);
    }
}
