using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class UserPermissionsTests
{
    [Fact]
    public async Task EffectivePermissions_UseUserOverrides_WhenPresent()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);

        var cashierRole = await db.Roles.SingleAsync(x => x.Name == AppRoles.Cashier);

        db.RolePermissions.Add(new RolePermission { RoleId = cashierRole.Id, Permission = AppPermissions.PosUse });
        db.RolePermissions.Add(new RolePermission { RoleId = cashierRole.Id, Permission = AppPermissions.ShiftsOpenResume });

        var hasher = new Pbkdf2PasswordHasher();
        var (hash, salt) = hasher.HashPassword("Password1!");
        var user = new User
        {
            Username = "cashier1",
            DisplayName = "Cashier One",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleId = cashierRole.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = AppPermissions.PosUse });
        db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = AppPermissions.DashboardView });
        await db.SaveChangesAsync();

        var perms = new PermissionService(db);
        var effective = await perms.GetEffectivePermissionsAsync(user.Id);

        Assert.Contains(AppPermissions.DashboardView, effective);
        Assert.DoesNotContain(AppPermissions.ShiftsOpenResume, effective);
        Assert.True(await perms.HasPermissionAsync(user.Id, AppPermissions.PosUse));
        Assert.False(await perms.HasPermissionAsync(user.Id, AppPermissions.ShiftsOpenResume));
    }

    [Fact]
    public async Task StoreSettings_Save_PersistsAppearanceAndPrintFlags()
    {
        var path = Path.Combine(Path.GetTempPath(), "storespos-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, "{}");
            var svc = new StoreSettingsService(
                TestFactories.StoreMonitor(new StoreSettings()),
                Options.Create(new StoreSettingsPersistenceOptions { FilePath = path }));

            await svc.SaveAsync(new StoreSettingsFormDto
            {
                NameEn = "Demo",
                NameAr = "تجريبي",
                PaperWidthMm = 58,
                DefaultReceiptFormat = "80mm",
                DefaultPrinterName = "XP-80",
                ShowLogoOn80mm = true,
                ShowBarcode = false,
                ReceiptFontScale = 1.1m,
                UiTheme = "dark",
                UiPrimaryColor = "#123456",
                InvoiceNumberPrefix = "S-",
                ReturnNumberPrefix = "R-",
                BackupFolder = @"C:\StoresPOS\Backups"
            });

            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("\"DefaultPrinterName\": \"XP-80\"", json, StringComparison.Ordinal);
            Assert.Contains("\"ShowBarcode\": false", json, StringComparison.Ordinal);
            Assert.Contains("\"UiTheme\": \"dark\"", json, StringComparison.Ordinal);
            Assert.Contains("\"UiPrimaryColor\": \"#123456\"", json, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task SeedRolesAsync(ApplicationDbContext db)
    {
        foreach (var name in AppRoles.All)
        {
            db.Roles.Add(new Role { Name = name, Description = name });
        }

        await db.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
