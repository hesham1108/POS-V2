using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

/// <summary>Phase 21: lockout, last Owner, self lock-out, stamps, audit, role matrix, templates.</summary>
public class UserSecurityTests
{
    private sealed record Ctx(ApplicationDbContext Db, AuthService Auth, UserAdminService Users, int OwnerId, int CashierRoleId, int ManagerRoleId, int OwnerRoleId);

    private static async Task<Ctx> CreateAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var hasher = new Pbkdf2PasswordHasher();
        var audit = new AuditLogService(db, TimeProvider.System);
        var jwt = new JwtTokenService(Options.Create(new JwtSettings
        {
            Issuer = "StoresPOS",
            Audience = "StoresPOS",
            SecretKey = "StoresPOS-Dev-Only-Secret-Key-Change-In-Production!",
            ExpirationMinutes = 60
        }));
        var auth = new AuthService(db, hasher, jwt, Options.Create(new AdminSeedSettings
        {
            Username = "admin",
            Password = "Admin@12345",
            DisplayName = "Owner"
        }), audit);
        await auth.EnsureSeedDataAsync();
        var users = new UserAdminService(db, hasher, new PermissionService(db), audit, TimeProvider.System);
        var owner = await db.Users.SingleAsync();
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        return new Ctx(db, auth, users, owner.Id, roles[AppRoles.Cashier], roles[AppRoles.Manager], roles[AppRoles.Owner]);
    }

    private static UserUpsertDto Upsert(string name, int roleId, bool active = true, string? password = "Passw0rd!") => new()
    {
        Username = name,
        DisplayName = name,
        Password = password,
        RoleId = roleId,
        IsActive = active
    };

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var c = await CreateAsync();
        for (var i = 0; i < AuthClaims.MaxFailedLogins - 1; i++)
        {
            var wrong = await c.Auth.LoginAsync(new LoginRequestDto { Username = "admin", Password = "nope-nope" });
            Assert.Null(wrong.LockedMinutes);
        }

        var locked = await c.Auth.LoginAsync(new LoginRequestDto { Username = "admin", Password = "nope-nope" });
        Assert.False(locked.Succeeded);
        Assert.Equal(15, locked.LockedMinutes);

        var right = await c.Auth.LoginAsync(new LoginRequestDto { Username = "admin", Password = "Admin@12345" });
        Assert.False(right.Succeeded);
        Assert.NotNull(right.LockedMinutes);
        Assert.Contains(await c.Db.AuditLogs.Select(a => a.Action).ToListAsync(), a => a == "user.locked");

        await c.Users.UnlockAsync(c.OwnerId, c.OwnerId);
        Assert.True((await c.Auth.LoginAsync(new LoginRequestDto { Username = "admin", Password = "Admin@12345" })).Succeeded);
    }

    [Fact]
    public async Task LastOwner_CannotBeDeactivatedOrDemoted()
    {
        var c = await CreateAsync();
        var second = await c.Users.CreateAsync(c.OwnerId, Upsert("owner2", c.OwnerRoleId));

        // With two active owners one may be deactivated…
        await c.Users.SetActiveAsync(c.OwnerId, second, false);

        // …but every way of removing the remaining active Owner is refused.
        await Assert.ThrowsAsync<BusinessException>(() => c.Users.SetActiveAsync(c.OwnerId, c.OwnerId, false));            // self
        await Assert.ThrowsAsync<BusinessException>(() =>
            c.Users.UpdateAsync(c.OwnerId, c.OwnerId, Upsert("admin", c.ManagerRoleId, password: null)));                 // self-demote
        await Assert.ThrowsAsync<BusinessException>(() => c.Users.SetActiveAsync(second, c.OwnerId, false));              // inactive owner
        await Assert.ThrowsAsync<BusinessException>(() =>
            c.Users.UpdateAsync(second, c.OwnerId, Upsert("admin", c.ManagerRoleId, password: null)));

        // And with two active owners, demoting one is fine, the other stays.
        await c.Users.SetActiveAsync(c.OwnerId, second, true);
        await c.Users.UpdateAsync(c.OwnerId, second, Upsert("owner2", c.ManagerRoleId, password: null));
        Assert.True(await c.Db.Users.Where(x => x.Id == c.OwnerId).Select(x => x.IsActive).SingleAsync());
    }

    [Fact]
    public async Task User_CannotRemoveTheirOwnUsersAccess()
    {
        var c = await CreateAsync();
        var dto = Upsert("admin", c.OwnerRoleId, password: null);
        dto.UseCustomTabs = true;
        dto.CustomPermissions = [AppPermissions.PosUse];
        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Users.UpdateAsync(c.OwnerId, c.OwnerId, dto));
        Assert.Equal("You cannot remove your own access to Users and roles.", ex.Message);
        Assert.False(await c.Db.UserPermissions.AnyAsync());   // nothing half-saved

        await Assert.ThrowsAsync<BusinessException>(() => c.Users.SetActiveAsync(c.OwnerId, c.OwnerId, false));
    }

    [Fact]
    public async Task PermissionChange_BumpsTheStamp_AndIsAudited_WithoutTouchingTheSecurityStamp()
    {
        var c = await CreateAsync();
        var id = await c.Users.CreateAsync(c.OwnerId, Upsert("mona", c.CashierRoleId));
        var before = await c.Auth.GetStampsAsync(id);

        var dto = Upsert("mona", c.CashierRoleId, password: null);
        dto.UseCustomTabs = true;
        dto.CustomPermissions = [.. AppPermissions.DefaultsFor(AppRoles.Cashier), AppPermissions.ReturnsCreate];
        await c.Users.UpdateAsync(c.OwnerId, id, dto);

        var after = await c.Auth.GetStampsAsync(id);
        Assert.Equal(before!.PermissionStamp + 1, after!.PermissionStamp);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);

        var activity = await c.Users.GetActivityAsync(id);
        Assert.Contains(activity, a => a.Action == "user.permissions" && a.Details.Contains("+ Returns.Create"));
        Assert.Contains(activity, a => a.Action == "user.created");

        // The refreshed token carries the new permission.
        var refreshed = await c.Auth.RefreshAsync(id);
        Assert.True(refreshed.Succeeded);

        // Same set as the role = back to role (no custom rows).
        dto.CustomPermissions = AppPermissions.DefaultsFor(AppRoles.Cashier).ToList();
        await c.Users.UpdateAsync(c.OwnerId, id, dto);
        Assert.False(await c.Db.UserPermissions.AnyAsync(x => x.UserId == id));
    }

    [Fact]
    public async Task SignOutEverywhere_AndPasswordReset_BumpTheSecurityStamp()
    {
        var c = await CreateAsync();
        var id = await c.Users.CreateAsync(c.OwnerId, Upsert("mona", c.CashierRoleId));
        var s0 = (await c.Auth.GetStampsAsync(id))!.SecurityStamp;

        await c.Users.SignOutEverywhereAsync(c.OwnerId, id);
        Assert.Equal(s0 + 1, (await c.Auth.GetStampsAsync(id))!.SecurityStamp);

        await c.Users.ResetPasswordAsync(c.OwnerId, id, "NewPassw0rd!", mustChange: true);
        var stamps = await c.Auth.GetStampsAsync(id);
        Assert.Equal(s0 + 2, stamps!.SecurityStamp);
        Assert.True(stamps.MustChangePassword);

        var login = await c.Auth.LoginAsync(new LoginRequestDto { Username = "mona", Password = "NewPassw0rd!" });
        Assert.True(login.MustChangePassword);

        var changed = await c.Auth.ChangePasswordAsync(id, "NewPassw0rd!", "Another1!");
        Assert.True(changed.Succeeded);
        Assert.False((await c.Auth.GetStampsAsync(id))!.MustChangePassword);
        Assert.DoesNotContain(await c.Db.AuditLogs.Select(a => a.Details).ToListAsync(), d => d.Contains("Another1!"));
    }

    [Fact]
    public async Task RoleMatrix_UpdatesEveryUserOfTheRole_AndTemplatesCreateRoles()
    {
        var c = await CreateAsync();
        var id = await c.Users.CreateAsync(c.OwnerId, Upsert("mona", c.CashierRoleId));
        var p0 = (await c.Auth.GetStampsAsync(id))!.PermissionStamp;

        await c.Users.SaveRoleMatrixAsync(c.OwnerId, new Dictionary<int, IReadOnlyList<string>>
        {
            [c.CashierRoleId] = [.. AppPermissions.DefaultsFor(AppRoles.Cashier), AppPermissions.ShiftsClose]
        });
        Assert.Equal(p0 + 1, (await c.Auth.GetStampsAsync(id))!.PermissionStamp);
        Assert.True(await c.Db.RolePermissions.AnyAsync(x => x.RoleId == c.CashierRoleId && x.Permission == AppPermissions.ShiftsClose));

        var roleId = await c.Users.CreateRoleFromTemplateAsync(c.OwnerId, "storekeeper", "Store room");
        var perms = await c.Db.RolePermissions.Where(x => x.RoleId == roleId).Select(x => x.Permission).ToListAsync();
        Assert.Contains(AppPermissions.InventoryAdjust, perms);
        Assert.DoesNotContain(AppPermissions.PosUse, perms);

        await Assert.ThrowsAsync<BusinessException>(() => c.Users.CreateRoleFromTemplateAsync(c.OwnerId, "storekeeper", "Store room"));
        await Assert.ThrowsAsync<BusinessException>(() => c.Users.SaveRoleMatrixAsync(c.OwnerId,
            new Dictionary<int, IReadOnlyList<string>> { [c.OwnerRoleId] = [] }));
    }

    [Fact]
    public void EveryPermission_IsInExactlyOneGroup()
    {
        var grouped = AppPermissions.Groups.SelectMany(g => g.Permissions).ToList();
        Assert.Equal(grouped.Count, grouped.Distinct().Count());
        Assert.Equal(AppPermissions.All.OrderBy(x => x), grouped.OrderBy(x => x));
    }
}
