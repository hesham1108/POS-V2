using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>
/// Phase 25 (plan §6, task 6): opens every screen in Arabic and fails on English words left in what the user
/// reads (text, placeholders, titles, aria-labels); and in English it fails on Arabic UI translations that
/// leaked in. Done in-process on the rendered HTML (no browser download); script-built text is covered by the
/// translated messages the views hand to the scripts (<c>data-msg-*</c>, <c>data-empty</c>, … attributes, scanned here too).
/// </summary>
public class ArabicPageScanTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ArabicPageScanTests(AppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Words that are the same in both languages: brands, codes, units, people and roles in the data.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "StoresPOS", "POS", "CSV", "PDF", "A4", "mm", "PIN", "InstaPay", "Vodafone", "Visa", "Cash", "Code", "JSON",
        "admin", "mona", "Mona", "System", "Admin", "Owner", "Manager", "Cashier", "Store", "Owner", "Paging",
        "pcs", "kg", "Al", "Noor", "Market", "EGP", "Excel", "SQL", "Server", "bak", "Device", "ID"
    };

    private static readonly Regex Scripts = new(@"<(script|style|svg|template)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex Comments = new(@"<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex Attr = new(@"\b(placeholder|title|aria-label|data-empty|data-dirty|data-clean|data-loading)=""([^""]*)""", RegexOptions.IgnoreCase);
    private static readonly Regex Tags = new(@"<[^>]+>");
    private static readonly Regex Word = new(@"(?<![\w-])[A-Za-z][A-Za-z’']{2,}(?![\w-])");

    /// <summary>Text the user can read on the page (and in key attributes); data in LTR number cells is kept.</summary>
    internal static string VisibleText(string html, string otherLanguage = "en")
    {
        var body = html;
        var start = body.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var head = start > 0 ? body[..start] : "";
        if (start > 0) body = body[start..];
        var sb = new StringBuilder();
        // The <title> counts too.
        var title = Regex.Match(head, "<title>(.*?)</title>", RegexOptions.Singleline);
        if (title.Success) sb.Append(title.Groups[1].Value).Append('\n');
        body = Comments.Replace(Scripts.Replace(body, " "), " ");
        // Text marked as the other language on purpose (both names of a record, the language switch).
        body = Regex.Replace(body, $@"<(\w+)[^>]*\blang=""{otherLanguage}""[^>]*>[^<]*</\1>", " ");
        foreach (Match m in Attr.Matches(body))
        {
            // Flags such as data-empty="true" are state, not text.
            if (m.Groups[2].Value is not ("true" or "false"))
            {
                sb.Append(m.Groups[2].Value).Append('\n');
            }
        }
        // <option value="en">English</option> and the language switch are meant to stay in their own language.
        body = Regex.Replace(body, @"<(option|button)[^>]*>\s*(English|العربية)\s*</\1>", " ");
        sb.Append(Tags.Replace(body, "\n"));
        return System.Net.WebUtility.HtmlDecode(sb.ToString());
    }

    private async Task<(HttpClient Client, List<string> Urls)> ArabicAdminAsync(string culture)
    {
        await _factory.ResetShiftsAsync();
        int productId, customerId, supplierId, chequeId, shiftId, invoiceId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            productId = (await db.Products.FirstAsync()).Id;
            var customer = await db.Customers.FirstOrDefaultAsync() ?? db.Customers.Add(new Customer { Name = "عميل", Phone = "0100", IsActive = true }).Entity;
            var supplier = await db.Suppliers.FirstOrDefaultAsync() ?? db.Suppliers.Add(new Supplier { NameAr = "مورد", NameEn = "Supplier one", IsActive = true }).Entity;
            await db.SaveChangesAsync();
            customerId = customer.Id;
            supplierId = supplier.Id;
            var admin = await db.Users.FirstAsync(x => x.Username == "admin");
            var shift = new Shift { OpenedByUserId = admin.Id, OpenedAtUtc = DateTime.UtcNow.AddHours(-1), Status = ShiftStatus.Closed, ClosedAtUtc = DateTime.UtcNow, ClosedByUserId = admin.Id, ExpectedCash = 100, ActualCash = 90, CashDifference = -10, Reconciliation = ShiftReconciliation.Shortage };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            db.ShiftParticipants.Add(new ShiftParticipant { ShiftId = shift.Id, UserId = admin.Id });
            var product = await db.Products.FirstAsync();
            var invoice = new SalesInvoice
            {
                InvoiceNumber = "S-800001", InvoiceSequence = 800001, Status = SalesInvoiceStatus.Completed, ShiftId = shift.Id, CashierUserId = admin.Id,
                Subtotal = 78m, Total = 78m, PaymentMethod = PaymentMethod.Cash, AmountPaid = 100, ChangeAmount = 22, CompletedAtUtc = DateTime.UtcNow,
                Items = [new SalesInvoiceItem { ProductId = product.Id, ProductNameAr = product.NameAr, ProductNameEn = product.NameEn, Unit = product.Unit, Quantity = 1, CatalogUnitPrice = 78, UnitPrice = 78, LineTotal = 78, UnitCost = 65 }]
            };
            db.SalesInvoices.Add(invoice);
            var cheque = new Cheque { Number = "C-1", Amount = 100, Direction = ChequeDirection.In, DueDate = DateTime.Today, CreatedByUserId = admin.Id };
            db.Cheques.Add(cheque);
            await db.SaveChangesAsync();
            chequeId = cheque.Id;
            shiftId = shift.Id;
            invoiceId = invoice.Id;
        }

        var client = _factory.CreateBrowser();
        client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c%3Den-US%7Cuic%3D{culture}");
        await LoginAsync(client, "admin", "Admin@12345");

        var urls = new List<string>
        {
            "/", "/Sales", $"/Sales/Details/{invoiceId}", "/Returns", "/Returns/Create", "/CashOuts", "/Customers", "/Customers/Create",
            $"/Customers/Details/{customerId}", $"/Customers/Edit/{customerId}", "/Products", "/Products/Create", $"/Products/Edit/{productId}",
            $"/Products/Details/{productId}", "/Categories", "/Categories/Create", "/Inventory", "/Inventory/Movements", "/Inventory/Adjust",
            "/Purchases", "/Purchases/Create", "/Suppliers", "/Suppliers/Create", $"/Suppliers/Details/{supplierId}", $"/Suppliers/Edit/{supplierId}",
            "/Warehouses", "/Warehouses/Create", "/Warehouses/Transfers", "/Warehouses/Transfer", "/Treasuries", "/Treasuries/Create",
            "/Treasuries/Transfer", "/Cheques", "/Cheques?direction=2", "/Cheques/Create", $"/Cheques/Details/{chequeId}",
            "/PaymentMethods", "/PaymentMethods/Create", "/Users", "/Users/Create", "/Users/Roles", "/Shifts/Index", $"/Shifts/Details/{shiftId}",
            "/Shifts/Gate", "/Account/ChangePassword", $"/Receipts/Sale/{invoiceId}", $"/Receipts/SaleA4/{invoiceId}", $"/Receipts/Shift/{shiftId}",
            $"/Receipts/Shift/{shiftId}?format=A4"
        };
        urls.AddRange(StoresPOS.PL.Controllers.SettingsController.Tabs.Select(t => "/Settings?tab=" + t));
        urls.AddRange(new[] { "Sales", "Profit", "Hours", "Products", "Audit", "Tax", "Purchases", "Inventory", "Shifts", "Drawer", "CashOuts", "Cheques", "ChequeAging" }.Select(r => "/Reports/" + r));
        urls.Add("/Home/Details");
        return (client, urls);
    }

    [Fact]
    public async Task EveryPage_InArabic_HasNoEnglishLeft()
    {
        var (client, urls) = await ArabicAdminAsync("ar");
        var problems = new SortedDictionary<string, SortedSet<string>>();
        foreach (var url in urls)
        {
            var page = await GetAsync(client, url);
            Assert.False(page.Url.Contains("/Account/Login"), $"{url} sent us to the login page");
            if (!url.StartsWith("/Home/Details"))
            {
                Assert.Matches("<html[^>]*lang=\"ar\"[^>]*dir=\"rtl\"|<html[^>]*dir=\"rtl\"", page.Html);
            }

            var words = Word.Matches(VisibleText(page.Html)).Select(m => m.Value).Where(w => !Allowed.Contains(w)).ToList();
            if (words.Count > 0)
            {
                problems[url] = new SortedSet<string>(words);
            }
        }

        // Signed-out pages too.
        var anon = _factory.CreateBrowser();
        anon.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        var login = await GetAsync(anon, "/Account/Login");
        var loginWords = Word.Matches(VisibleText(login.Html)).Select(m => m.Value).Where(w => !Allowed.Contains(w)).ToList();
        if (loginWords.Count > 0) problems["/Account/Login"] = new SortedSet<string>(loginWords);

        Assert.True(problems.Count == 0,
            "English left in the Arabic UI:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems.Select(p => $"{p.Key}: {string.Join(", ", p.Value)}")));
    }

    [Fact]
    public async Task EveryPage_InEnglish_HasNoArabicTranslationLeaked()
    {
        var resx = XDocument.Load(Path.Combine(RepoRoot(), "src", "StoresPOS.PL", "Resources", "SharedResource.ar.resx"));
        // Longer translations only: short words (e.g. "نعم") also appear in data.
        var arabic = resx.Root!.Elements("data").Select(d => d.Element("value")?.Value ?? "")
            .Where(v => v.Length >= 8 && !v.Contains('{'))
            .ToHashSet();
        var (client, urls) = await ArabicAdminAsync("en");
        var problems = new List<string>();
        // Printed documents use the store's receipt language on purpose (Arabic by default).
        foreach (var url in urls.Where(u => !u.StartsWith("/Receipts", StringComparison.Ordinal)))
        {
            var text = VisibleText((await GetAsync(client, url)).Html, otherLanguage: "ar");
            foreach (var leak in arabic.Where(text.Contains).Take(5))
            {
                problems.Add($"{url}: {leak}");
            }
        }

        Assert.True(problems.Count == 0, "Arabic UI text in the English UI:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StoresPOS.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
