using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.Tests;

/// <summary>
/// End-to-end HTTP checks for journeys A, B and H against the real MVC pipeline
/// (auth cookie, permission policies, antiforgery, views) on the EF InMemory provider.
/// </summary>
[Collection("app")]
public class ShiftJourneyTests : IClassFixture<ShiftJourneyTests.AppFactory>
{
    internal const string CashierPassword = "Cashier@12345";
    private readonly AppFactory _factory;

    public ShiftJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cashier_OpensShift_SecondUserJoins_ManagerCloses()
    {
        await _factory.ResetShiftsAsync();

        // A — cashier logs in, lands on the gate (not the dashboard), opens with a blank amount.
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);
        Assert.Contains("/Shifts/Gate", gate.Url);
        Assert.Contains("Open a shift to start selling", gate.Html);

        var pos = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "" });
        Assert.Contains("/Pos", pos.Url);
        Assert.Contains("Pay · F9", pos.Html);

        var current = await GetAsync(mona, "/Shifts/Current");
        Assert.Contains("Shift #", current.Html);
        Assert.Contains("0.00", current.Html);

        // Cashier has no Shifts.Close by default.
        var denied = await GetAsync(mona, "/Shifts/Close");
        Assert.Contains("/Account/AccessDenied", denied.Url);

        // Cashier cannot reach back-office screens.
        var products = await GetAsync(mona, "/Products");
        Assert.Contains("/Account/AccessDenied", products.Url);

        // B — admin (not a participant yet) sees "already open" and resumes.
        var admin = _factory.CreateBrowser();
        var dashboard = await LoginAsync(admin, "admin", "Admin@12345");
        Assert.DoesNotContain("/Shifts/Gate", dashboard.Url);
        var adminGate = await GetAsync(admin, "/Shifts/Gate");
        Assert.Contains("A shift is already open", adminGate.Html);
        var joined = await PostFormAsync(admin, "/Shifts/Resume", adminGate.Html, new());
        Assert.Contains("/Pos", joined.Url);
        var shiftPage = await GetAsync(admin, "/Shifts/Current");
        Assert.Contains("Mona", shiftPage.Html);
        Assert.Contains("System Admin", shiftPage.Html);

        // Second visit to the gate for a participant goes straight through to the till.
        var through = await GetAsync(mona, "/Shifts/Gate");
        Assert.Contains("/Pos", through.Url);

        // H — admin counts 20.00 over → Surplus, signed out to login with a notice.
        var close = await GetAsync(admin, "/Shifts/Close");
        Assert.Contains("Confirm and close shift", close.Html);
        var shiftId = Regex.Match(close.Html, "name=\"ShiftId\" value=\"(\\d+)\"").Groups[1].Value;
        var login = await PostFormAsync(admin, "/Shifts/Close", close.Html, new()
        {
            ["ShiftId"] = shiftId,
            ["ActualCash"] = "20.00",
            ["ReturnTo"] = "",
            ["Notes"] = ""
        });
        Assert.Contains("/Account/Login", login.Url);
        Assert.Contains($"Shift #{shiftId} closed.", login.Html);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shift = await db.Shifts.AsNoTracking().SingleAsync(x => x.Id == int.Parse(shiftId));
        Assert.Equal(ShiftStatus.Closed, shift.Status);
        Assert.Equal(ShiftReconciliation.Surplus, shift.Reconciliation);
        Assert.Equal(20m, shift.CashDifference);

        // Back at the gate the next cashier starts a new shift (A again).
        var again = await GetAsync(mona, "/Shifts/Gate");
        Assert.Contains("Open a shift to start selling", again.Html);
    }

    [Fact]
    public async Task Gate_NegativeOpeningAmount_ShowsError()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        var gate = await LoginAsync(mona, "mona", CashierPassword);

        var result = await PostFormAsync(mona, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "-5" });

        Assert.Contains("Opening amount must be zero or more.", result.Html);
    }

    [Fact]
    public async Task Gate_InArabic_IsRtlAndTranslated()
    {
        await _factory.ResetShiftsAsync();
        var mona = _factory.CreateBrowser();
        mona.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        var gate = await LoginAsync(mona, "mona", CashierPassword);

        Assert.Contains("dir=\"rtl\"", gate.Html);
        Assert.Contains("bootstrap.rtl.min.css", gate.Html);
        Assert.Contains("افتح وردية لبدء البيع", gate.Html);
        Assert.Contains("الورديات", gate.Html); // sidebar
    }

    internal sealed record Page(string Url, string Html);

    internal static async Task<Page> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        return new Page(response.RequestMessage!.RequestUri!.PathAndQuery, await response.Content.ReadAsStringAsync());
    }

    internal static async Task<Page> LoginAsync(HttpClient client, string username, string password)
    {
        var form = await GetAsync(client, "/Account/Login");
        return await PostFormAsync(client, "/Account/Login", form.Html, new()
        {
            ["Username"] = username,
            ["Password"] = password
        });
    }

    internal static async Task<Page> PostFormAsync(
        HttpClient client,
        string url,
        string pageWithToken,
        Dictionary<string, string> fields)
    {
        var token = Regex.Match(pageWithToken, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "antiforgery token not found");
        fields["__RequestVerificationToken"] = token;

        var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        return new Page(response.RequestMessage!.RequestUri!.PathAndQuery, await response.Content.ReadAsStringAsync());
    }

    public sealed class AppFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "pl-tests-" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                                || d.ServiceType == typeof(DbContextOptions)
                                || (d.ServiceType.IsGenericType
                                    && d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")))
                    .ToList();
                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));
            });
        }

        public HttpClient CreateBrowser() =>
            CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

        public async Task ResetShiftsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ShiftParticipants.RemoveRange(db.ShiftParticipants);
            db.Shifts.RemoveRange(db.Shifts);

            db.SalesInvoiceItems.RemoveRange(db.SalesInvoiceItems);
            db.SalesInvoices.RemoveRange(db.SalesInvoices);
            db.StockMovements.RemoveRange(db.StockMovements);

            var oil = await db.Products.FirstOrDefaultAsync(x => x.Barcode == "622100");
            if (oil is null)
            {
                db.Products.Add(new Product
                {
                    NameAr = "زيت عباد الشمس ١ لتر", NameEn = "Sunflower oil 1 L", Barcode = "622100", Unit = "L",
                    PurchasePrice = 65m, RetailPrice = 78m, WholesalePrice = 72m,
                    StockQuantity = 20m, LowStockThreshold = 5m
                });
            }
            else
            {
                oil.StockQuantity = 20m;
            }

            if (!await db.Users.AnyAsync(x => x.Username == "mona"))
            {
                var role = await db.Roles.SingleAsync(x => x.Name == AppRoles.Cashier);
                var (hash, salt) = new Pbkdf2PasswordHasher().HashPassword(CashierPassword);
                db.Users.Add(new User
                {
                    Username = "mona",
                    DisplayName = "Mona",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    RoleId = role.Id
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
