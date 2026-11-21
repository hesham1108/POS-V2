using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoresPOS.BLL.Constants;
using StoresPOS.DAL.Context;
using static StoresPOS.PL.Tests.ShiftJourneyTests;

namespace StoresPOS.PL.Tests;

/// <summary>Phase 21: permission changes apply without signing in again; security actions sign the user out.</summary>
public class UserPermissionsJourneyTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public UserPermissionsJourneyTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Admin, HttpClient Mona, int MonaId, int CashierRoleId)> SetUpAsync()
    {
        await _factory.ResetShiftsAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mona = await db.Users.SingleAsync(x => x.Username == "mona");
        db.UserPermissions.RemoveRange(db.UserPermissions.Where(x => x.UserId == mona.Id));
        mona.IsActive = true;
        mona.MustChangePassword = false;
        var (hash, salt) = new StoresPOS.BLL.Services.Pbkdf2PasswordHasher().HashPassword(CashierPassword);
        mona.PasswordHash = hash;
        mona.PasswordSalt = salt;
        await db.SaveChangesAsync();

        var admin = _factory.CreateBrowser();
        admin.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(admin, "admin", "Admin@12345");
        var monaClient = _factory.CreateBrowser();
        monaClient.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        await LoginAsync(monaClient, "mona", CashierPassword);
        return (admin, monaClient, mona.Id, mona.RoleId);
    }

    private static async Task<Page> PostAsync(HttpClient client, string url, string pageWithToken, List<KeyValuePair<string, string>> fields)
    {
        var token = Regex.Match(pageWithToken, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        fields.Add(new("__RequestVerificationToken", token));
        var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        return new Page(response.RequestMessage!.RequestUri!.PathAndQuery, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GrantedPermission_AppliesOnTheNextRequest_WithoutSigningInAgain()
    {
        var (admin, mona, monaId, cashierRoleId) = await SetUpAsync();
        Assert.Contains("/Account/AccessDenied", (await GetAsync(mona, "/Customers")).Url);

        var edit = await GetAsync(admin, $"/Users/Edit/{monaId}?tab=permissions");
        Assert.Contains("data-perm-group=\"sales\"", edit.Html);
        Assert.Contains("What this user will see", edit.Html);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("Id", monaId.ToString()),
            new("Tab", "permissions"),
            new("Username", "mona"),
            new("DisplayName", "Mona"),
            new("RoleId", cashierRoleId.ToString()),
            new("IsActive", "true"),
            new("UseCustomTabs", "true")
        };
        foreach (var p in AppPermissions.DefaultsFor(AppRoles.Cashier).Append(AppPermissions.ReportsView))
        {
            fields.Add(new("SelectedPermissions", p));
        }

        var saved = await PostAsync(admin, "/Users/Save", edit.Html, fields);
        Assert.Contains($"/Users/Edit/{monaId}", saved.Url);
        Assert.Contains("Custom", saved.Html);

        // Same browser session, no new sign-in: the cookie is re-issued with the new permission.
        var customers = await GetAsync(mona, "/Customers");
        Assert.DoesNotContain("/Account/AccessDenied", customers.Url);
        Assert.Contains("/Customers", customers.Url);

        var activity = await GetAsync(admin, $"/Users/Edit/{monaId}?tab=activity");
        Assert.Contains("Reports.View", activity.Html);
    }

    [Fact]
    public async Task SignOutEverywhere_EndsTheOtherSession()
    {
        var (admin, mona, monaId, _) = await SetUpAsync();
        Assert.Contains("/Shifts/Gate", (await GetAsync(mona, "/Shifts/Gate")).Url);

        var edit = await GetAsync(admin, $"/Users/Edit/{monaId}?tab=security");
        await PostAsync(admin, $"/Users/SignOutEverywhere/{monaId}", edit.Html, []);

        Assert.Contains("/Account/Login", (await GetAsync(mona, "/Shifts/Gate")).Url);
    }

    [Fact]
    public async Task PasswordReset_WithMustChange_SendsTheUserToChangePassword()
    {
        var (admin, _, monaId, _) = await SetUpAsync();
        var edit = await GetAsync(admin, $"/Users/Edit/{monaId}?tab=security");
        await PostAsync(admin, $"/Users/ResetPassword/{monaId}", edit.Html,
            [new("newPassword", "Temporary1!"), new("mustChange", "true")]);

        var mona = _factory.CreateBrowser();
        mona.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den");
        var afterLogin = await LoginAsync(mona, "mona", "Temporary1!");
        Assert.Contains("/Account/ChangePassword", afterLogin.Url);
        Assert.Contains("/Account/ChangePassword", (await GetAsync(mona, "/Shifts/Gate")).Url);

        var changed = await PostAsync(mona, "/Account/ChangePassword", afterLogin.Html,
        [
            new("CurrentPassword", "Temporary1!"),
            new("NewPassword", "MyOwnPass1!"),
            new("ConfirmPassword", "MyOwnPass1!")
        ]);
        Assert.DoesNotContain("/Account/ChangePassword", changed.Url);
        Assert.DoesNotContain("/Account/ChangePassword", (await GetAsync(mona, "/Shifts/Gate")).Url);
    }

    [Fact]
    public async Task UsersPages_Open_InEnglishAndArabic()
    {
        var (admin, _, monaId, _) = await SetUpAsync();
        var list = await GetAsync(admin, "/Users?q=mon");
        Assert.Contains("Mona", list.Html);
        Assert.Contains("sp-avatar", list.Html);
        Assert.Contains("Sign out on every device", list.Html);
        Assert.Contains("data-matrix-row", (await GetAsync(admin, "/Users/Roles")).Html);

        var ar = _factory.CreateBrowser();
        ar.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dar");
        await LoginAsync(ar, "admin", "Admin@12345");
        var arEdit = await GetAsync(ar, $"/Users/Edit/{monaId}?tab=permissions");
        Assert.Contains("dir=\"rtl\"", arEdit.Html);
        Assert.DoesNotContain(">What this user will see<", arEdit.Html);
        Assert.DoesNotContain(">Role and permissions<", arEdit.Html);
    }
}
