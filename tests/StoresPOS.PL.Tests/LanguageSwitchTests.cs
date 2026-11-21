using System.Text.RegularExpressions;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 25 (plan §6 task 7): the language switch works from any page and the choice sticks (cookie).</summary>
public class LanguageSwitchTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public LanguageSwitchTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwitchOnTheLoginPage_StaysThroughSignInAndEveryPage_IncludingThePos()
    {
        await _factory.ResetShiftsAsync();
        var browser = _factory.CreateBrowser();

        var login = await GetAsync(browser, "/Account/Login");
        Assert.Contains("lang=\"en\"", login.Html);
        Assert.Contains(">العربية<", login.Html);

        // Same switch as inside the app (server cookie), no script needed.
        var arabicLogin = await PostFormAsync(browser, "/Culture/Set", login.Html, new() { ["ui"] = "ar", ["returnUrl"] = "/Account/Login" });
        Assert.Contains("/Account/Login", arabicLogin.Url);
        Assert.Contains("dir=\"rtl\"", arabicLogin.Html);
        Assert.Contains("تسجيل الدخول", arabicLogin.Html);
        Assert.Contains("bootstrap.rtl.min.css", arabicLogin.Html);

        var home = await PostFormAsync(browser, "/Account/Login", arabicLogin.Html, new() { ["Username"] = "admin", ["Password"] = "Admin@12345" });
        Assert.Contains("dir=\"rtl\"", home.Html);
        Assert.Contains("dir=\"rtl\"", (await GetAsync(browser, "/Products")).Html);
        Assert.Contains("dir=\"rtl\"", (await GetAsync(browser, "/Settings?tab=templates")).Html);

        // The till in Arabic, no English left in what the cashier reads.
        var gate = await GetAsync(browser, "/Shifts/Gate");
        var pos = await PostFormAsync(browser, "/Shifts/Open", gate.Html, new() { ["OpeningAmount"] = "" });
        Assert.Contains("/Pos", pos.Url);
        Assert.Contains("dir=\"rtl\"", pos.Html);
        var english = Regex.Matches(ArabicPageScanTests.VisibleText(pos.Html), @"(?<![\w-])[A-Za-z][A-Za-z']{2,}(?![\w-])")
            .Select(m => m.Value)
            .Where(w => w is not ("StoresPOS" or "POS" or "InstaPay" or "Vodafone" or "Cash" or "Visa" or "admin" or "System" or "Admin" or "Owner" or "EGP" or "Esc" or "Enter" or "Del" or "Sunflower" or "oil"))
            .Distinct()
            .ToList();
        Assert.True(english.Count == 0, "English on the Arabic POS: " + string.Join(", ", english));

        // Back to English from the till.
        var en = await PostFormAsync(browser, "/Culture/Set", pos.Html, new() { ["ui"] = "en", ["returnUrl"] = "/Pos" });
        Assert.Contains("dir=\"ltr\"", en.Html);
        Assert.Contains("dir=\"ltr\"", (await GetAsync(browser, "/Products")).Html);
    }
}
