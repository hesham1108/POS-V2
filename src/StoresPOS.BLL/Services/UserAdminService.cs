using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

/// <summary>
/// Users, roles and permissions (Phase 21). Every change is written to the audit log; permission
/// changes bump the user's permission stamp (applied without signing in again) and security
/// actions bump the security stamp (signs the user out everywhere).
/// </summary>
public class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogService _audit;
    private readonly TimeProvider _time;

    public UserAdminService(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IPermissionService permissions,
        IAuditLogService audit,
        TimeProvider time)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _permissions = permissions;
        _audit = audit;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ read

    public Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
        ListAsync(new UserListFilterDto(), cancellationToken);

    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(UserListFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.Username.Contains(term) || x.DisplayName.Contains(term));
        }

        if (filter.RoleId.HasValue)
        {
            query = query.Where(x => x.RoleId == filter.RoleId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        var now = Now;
        return await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.DisplayName)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                RoleId = u.RoleId,
                RoleName = u.Role.Name,
                IsActive = u.IsActive,
                HasCustomTabs = u.Permissions.Any(),
                LastLoginAtUtc = u.LastLoginAtUtc,
                IsLocked = u.LockedUntilUtc != null && u.LockedUntilUtc > now,
                MustChangePassword = u.MustChangePassword
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var custom = await _db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == id).Select(x => x.Permission).ToListAsync(cancellationToken);
        var rolePerms = await _db.RolePermissions.AsNoTracking()
            .Where(x => x.RoleId == user.RoleId).Select(x => x.Permission).ToListAsync(cancellationToken);

        return new UserDetailsDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            RoleId = user.RoleId,
            RoleName = user.Role.Name,
            IsActive = user.IsActive,
            UseCustomTabs = custom.Count > 0,
            CustomPermissions = custom,
            RolePermissions = rolePerms,
            EffectivePermissions = await _permissions.GetEffectivePermissionsAsync(id, cancellationToken),
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc,
            HasApprovalPin = !string.IsNullOrEmpty(user.ApprovalPinHash),
            MustChangePassword = user.MustChangePassword,
            LockedUntilUtc = user.LockedUntilUtc is { } until && until > Now ? until : null
        };
    }

    public async Task<IReadOnlyList<RoleListItemDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name == AppRoles.Owner ? 0 : x.Name == AppRoles.Admin ? 1 : x.Name == AppRoles.Manager ? 2 : x.Name == AppRoles.Cashier ? 3 : 4)
            .ThenBy(x => x.Name)
            .Select(x => new RoleListItemDto { Id = x.Id, Name = x.Name, Description = x.Description })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermissionMatrixDto>> GetRoleMatricesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(x => x.Permissions)
            .ToListAsync(cancellationToken);
        var counts = await _db.Users.AsNoTracking()
            .GroupBy(x => x.RoleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return roles
            .OrderBy(r => r.Name == AppRoles.Owner ? 0 : r.Name == AppRoles.Admin ? 1 : r.Name == AppRoles.Manager ? 2 : r.Name == AppRoles.Cashier ? 3 : 4)
            .ThenBy(r => r.Name)
            .Select(r => new RolePermissionMatrixDto
            {
                RoleId = r.Id,
                RoleName = r.Name,
                IsLocked = r.Name is AppRoles.Owner or AppRoles.Admin,
                Permissions = r.Permissions.Select(p => p.Permission).OrderBy(x => x).ToList(),
                UserCount = counts.GetValueOrDefault(r.Id)
            }).ToList();
    }

    public Task<IReadOnlyList<AuditEntryDto>> GetActivityAsync(int userId, CancellationToken cancellationToken = default) =>
        _audit.ListForUserAsync(userId, 100, cancellationToken);

    // ------------------------------------------------------------------ users

    public async Task<int> CreateAsync(int actorUserId, UserUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        ValidateUpsert(dto, isCreate: true);
        var role = await RequireAssignableRoleAsync(actor, dto.RoleId, cancellationToken);

        if (await _db.Users.AnyAsync(x => x.Username == dto.Username.Trim(), cancellationToken))
        {
            throw new BusinessException("Username is already taken.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(dto.Password!);
        var user = new User
        {
            Username = dto.Username.Trim(),
            DisplayName = dto.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleId = role.Id,
            IsActive = dto.IsActive,
            MustChangePassword = dto.MustChangePassword,
            CreatedAtUtc = Now
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var custom = await ReplaceUserPermissionsAsync(actor, user, role, dto, cancellationToken);
        await _audit.AddAsync(actor.Id, "user.created", "User", user.Id, user.Id,
            $"Created {user.Username} ({user.DisplayName}) as {role.Name}"
            + (custom is null ? "." : $" with custom permissions: {string.Join(", ", custom)}."), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task UpdateAsync(int actorUserId, int userId, UserUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        ValidateUpsert(dto, isCreate: false);

        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);

        var role = await RequireAssignableRoleAsync(actor, dto.RoleId, cancellationToken);
        if (user.Id == actor.Id && AppRoles.IsOwner(actor.Role.Name) && !AppRoles.IsOwner(role.Name))
        {
            throw new BusinessException("Owner cannot demote their own account.");
        }

        if (user.Id == actor.Id && !dto.IsActive)
        {
            throw new BusinessException("You cannot deactivate your own account.");
        }

        await EnsureAnotherOwnerRemainsAsync(user, stillOwner: AppRoles.IsOwner(role.Name), stillActive: dto.IsActive, cancellationToken);

        var username = dto.Username.Trim();
        if (await _db.Users.AnyAsync(x => x.Username == username && x.Id != userId, cancellationToken))
        {
            throw new BusinessException("Username is already taken.");
        }

        var before = await _permissions.GetEffectivePermissionsAsync(user.Id, cancellationToken);
        var after = await ProspectiveEffectiveAsync(dto, role.Id, cancellationToken);
        if (user.Id == actor.Id && !after.Contains(AppPermissions.UsersManage))
        {
            throw new BusinessException("You cannot remove your own access to Users and roles.");
        }

        var changes = new List<string>();
        if (user.Username != username) changes.Add($"username {user.Username} → {username}");
        if (user.DisplayName != dto.DisplayName.Trim()) changes.Add($"name {user.DisplayName} → {dto.DisplayName.Trim()}");
        if (user.RoleId != role.Id) changes.Add($"role {user.Role.Name} → {role.Name}");
        if (user.IsActive != dto.IsActive) changes.Add(dto.IsActive ? "activated" : "deactivated");

        var deactivated = user.IsActive && !dto.IsActive;
        user.Username = username;
        user.DisplayName = dto.DisplayName.Trim();
        user.RoleId = role.Id;
        user.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            SetPassword(user, dto.Password, dto.MustChangePassword && user.Id != actor.Id);
            user.SecurityStamp++;
            changes.Add("password reset");
        }

        if (deactivated)
        {
            user.SecurityStamp++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ReplaceUserPermissionsAsync(actor, user, role, dto, cancellationToken);

        var added = after.Except(before).OrderBy(x => x).ToList();
        var removed = before.Except(after).OrderBy(x => x).ToList();
        if (added.Count > 0 || removed.Count > 0)
        {
            user.PermissionStamp++;
            await _audit.AddAsync(actor.Id, "user.permissions", "User", user.Id, user.Id,
                PermissionChangeText(added, removed), cancellationToken);
        }

        if (changes.Count > 0)
        {
            await _audit.AddAsync(actor.Id, "user.updated", "User", user.Id, user.Id, string.Join("; ", changes) + ".", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int actorUserId, int userId, bool active, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);
        if (user.IsActive == active)
        {
            return;
        }

        if (user.Id == actor.Id && !active)
        {
            throw new BusinessException("You cannot deactivate your own account.");
        }

        await EnsureAnotherOwnerRemainsAsync(user, stillOwner: AppRoles.IsOwner(user.Role.Name), stillActive: active, cancellationToken);
        user.IsActive = active;
        if (!active)
        {
            user.SecurityStamp++;
        }

        await _audit.AddAsync(actor.Id, active ? "user.activated" : "user.deactivated", "User", user.Id, user.Id,
            active ? "Activated." : "Deactivated (signed out everywhere).", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SignOutEverywhereAsync(int actorUserId, int userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);
        user.SecurityStamp++;
        await _audit.AddAsync(actor.Id, "user.signout", "User", user.Id, user.Id, "Signed out on every device.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(
        int actorUserId,
        int userId,
        string newPassword,
        bool mustChange,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);
        SetPassword(user, newPassword, mustChange && user.Id != actor.Id);
        user.SecurityStamp++;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        await _audit.AddAsync(actor.Id, "user.password", "User", user.Id, user.Id,
            mustChange ? "Password reset; must change it at next sign-in." : "Password reset.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetMustChangePasswordAsync(int actorUserId, int userId, bool mustChange, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);
        if (user.MustChangePassword == mustChange)
        {
            return;
        }

        user.MustChangePassword = mustChange;
        user.PermissionStamp++; // re-issue the token so the requirement applies right away
        await _audit.AddAsync(actor.Id, "user.password", "User", user.Id, user.Id,
            mustChange ? "Must change password at next sign-in." : "Password change no longer required.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlockAsync(int actorUserId, int userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);
        user.LockedUntilUtc = null;
        user.FailedLoginCount = 0;
        await _audit.AddAsync(actor.Id, "user.unlocked", "User", user.Id, user.Id, "Unlocked.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetApprovalPinAsync(int actorUserId, int userId, string? pin, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new BusinessException("User not found.");
        EnsureCanManageTarget(actor, user);

        if (string.IsNullOrWhiteSpace(pin))
        {
            user.ApprovalPinHash = null;
            user.ApprovalPinSalt = null;
            await _audit.AddAsync(actor.Id, "user.pin", "User", user.Id, user.Id, "Approval PIN removed.", cancellationToken);
        }
        else
        {
            var clean = pin.Trim();
            if (clean.Length is < 4 or > 6 || !clean.All(char.IsAsciiDigit))
            {
                throw new BusinessException("PIN must be 4 to 6 digits.");
            }

            var (hash, salt) = _passwordHasher.HashPassword(clean);
            user.ApprovalPinHash = hash;
            user.ApprovalPinSalt = salt;
            await _audit.AddAsync(actor.Id, "user.pin", "User", user.Id, user.Id, "Approval PIN set.", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ roles

    public Task SaveRolePermissionsAsync(
        int actorUserId,
        int roleId,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default) =>
        SaveRoleMatrixAsync(actorUserId, new Dictionary<int, IReadOnlyList<string>> { [roleId] = permissions }, cancellationToken);

    /// <summary>Saves several roles at once (the roles × permissions matrix).</summary>
    public async Task SaveRoleMatrixAsync(
        int actorUserId,
        IReadOnlyDictionary<int, IReadOnlyList<string>> rolePermissions,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        if (!AppRoles.IsOwnerOrAdmin(actor.Role.Name))
        {
            throw new BusinessException("Only Owner or Admin can edit role defaults.");
        }

        foreach (var (roleId, permissions) in rolePermissions)
        {
            var role = await _db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken)
                ?? throw new BusinessException("Role not found.");
            if (role.Name is AppRoles.Owner or AppRoles.Admin)
            {
                throw new BusinessException("Owner and Admin permission matrices are locked.");
            }

            var before = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);
            var clean = permissions
                .Where(p => AppPermissions.All.Contains(p, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var added = clean.Where(p => !before.Contains(p)).OrderBy(x => x).ToList();
            var removed = before.Where(p => !clean.Contains(p)).OrderBy(x => x).ToList();
            if (added.Count == 0 && removed.Count == 0)
            {
                continue;
            }

            if (role.Id == actor.Role.Id && removed.Contains(AppPermissions.UsersManage) && !await HasCustomAsync(actor.Id, cancellationToken))
            {
                throw new BusinessException("You cannot remove your own access to Users and roles.");
            }

            _db.RolePermissions.RemoveRange(role.Permissions.Where(p => removed.Contains(p.Permission)));
            foreach (var p in added)
            {
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = p });
            }

            // Everyone on this role gets the change on their next request.
            await _db.Users.Where(x => x.RoleId == role.Id).ForEachAsync(u => u.PermissionStamp++, cancellationToken);
            await _audit.AddAsync(actor.Id, "role.permissions", "Role", role.Id, null,
                $"{role.Name}: {PermissionChangeText(added, removed)}", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>New role copied from a ready-made template (Cashier, Manager, Accountant, Storekeeper).</summary>
    public async Task<int> CreateRoleFromTemplateAsync(
        int actorUserId,
        string templateKey,
        string name,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(actorUserId, cancellationToken);
        if (!AppRoles.IsOwnerOrAdmin(actor.Role.Name))
        {
            throw new BusinessException("Only Owner or Admin can edit role defaults.");
        }

        var template = AppPermissions.RoleTemplates.FirstOrDefault(t => t.Key == templateKey);
        if (template.Key is null)
        {
            throw new BusinessException("Role template not found.");
        }

        var clean = (name ?? string.Empty).Trim();
        if (clean.Length is 0 or > 50)
        {
            throw new BusinessException("Role name is required (50 characters max).");
        }

        if (await _db.Roles.AnyAsync(x => x.Name == clean, cancellationToken))
        {
            throw new BusinessException("A role with this name already exists.");
        }

        var role = new Role { Name = clean, Description = $"From template: {template.NameEn}" };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var p in template.Permissions)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = p });
        }

        await _audit.AddAsync(actor.Id, "role.created", "Role", role.Id, null,
            $"Created role {clean} from template {template.NameEn}.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Saves the user's custom permissions; returns the custom list, or null when the role's set is used.</summary>
    private async Task<IReadOnlyList<string>?> ReplaceUserPermissionsAsync(
        ActorInfo actor,
        User user,
        Role role,
        UserUpsertDto dto,
        CancellationToken cancellationToken)
    {
        var existing = await _db.UserPermissions.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken);
        _db.UserPermissions.RemoveRange(existing);

        var clean = dto.CustomPermissions
            .Where(p => AppPermissions.All.Contains(p, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Same set as the role = no custom list ("back to role").
        var rolePerms = await _db.RolePermissions.AsNoTracking()
            .Where(x => x.RoleId == role.Id).Select(x => x.Permission).ToListAsync(cancellationToken);
        var sameAsRole = clean.Count == rolePerms.Count && clean.All(rolePerms.Contains);

        if (!dto.UseCustomTabs || sameAsRole)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (AppRoles.IsOwnerOrAdmin(role.Name) && !AppRoles.IsOwner(actor.Role.Name))
        {
            throw new BusinessException("Only Owner can set custom tabs for Admin users.");
        }

        foreach (var p in clean)
        {
            _db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = p });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return clean;
    }

    /// <summary>What the user will be allowed after this save (custom list, or the new role's set).</summary>
    private async Task<HashSet<string>> ProspectiveEffectiveAsync(UserUpsertDto dto, int roleId, CancellationToken cancellationToken)
    {
        if (dto.UseCustomTabs)
        {
            return dto.CustomPermissions
                .Where(p => AppPermissions.All.Contains(p, StringComparer.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
        }

        var role = await _db.RolePermissions.AsNoTracking().Where(x => x.RoleId == roleId).Select(x => x.Permission).ToListAsync(cancellationToken);
        return role.ToHashSet(StringComparer.Ordinal);
    }

    private Task<bool> HasCustomAsync(int userId, CancellationToken cancellationToken) =>
        _db.UserPermissions.AnyAsync(x => x.UserId == userId, cancellationToken);

    /// <summary>At least one active Owner must always remain.</summary>
    private async Task EnsureAnotherOwnerRemainsAsync(User user, bool stillOwner, bool stillActive, CancellationToken cancellationToken)
    {
        var isOwnerNow = AppRoles.IsOwner(user.Role.Name) && user.IsActive;
        if (!isOwnerNow || (stillOwner && stillActive))
        {
            return;
        }

        var others = await _db.Users.CountAsync(
            x => x.Id != user.Id && x.IsActive && x.Role.Name == AppRoles.Owner, cancellationToken);
        if (others == 0)
        {
            throw new BusinessException("There must be at least one active Owner.");
        }
    }

    private void SetPassword(User user, string password, bool mustChange)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new BusinessException("Password must be at least 8 characters.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = mustChange;
    }

    private static string PermissionChangeText(IReadOnlyList<string> added, IReadOnlyList<string> removed)
    {
        var parts = new List<string>();
        if (added.Count > 0) parts.Add("+ " + string.Join(", ", added));
        if (removed.Count > 0) parts.Add("− " + string.Join(", ", removed));
        return string.Join("; ", parts);
    }

    private static void ValidateUpsert(UserUpsertDto dto, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            throw new BusinessException("Username and display name are required.");
        }

        if (isCreate && (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8))
        {
            throw new BusinessException("Password must be at least 8 characters.");
        }
    }

    private async Task<ActorInfo> RequireActorAsync(int actorUserId, CancellationToken cancellationToken)
    {
        var actor = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken)
            ?? throw new BusinessException("Actor not found.");

        if (!await _permissions.HasPermissionAsync(actorUserId, AppPermissions.UsersManage, cancellationToken))
        {
            throw new BusinessException("You do not have permission to manage users.");
        }

        return new ActorInfo(actor.Id, actor.Role);
    }

    private async Task<Role> RequireAssignableRoleAsync(ActorInfo actor, int roleId, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken)
            ?? throw new BusinessException("Role not found.");

        if (AppRoles.IsOwner(role.Name) && !AppRoles.IsOwner(actor.Role.Name))
        {
            throw new BusinessException("Only Owner can assign the Owner role.");
        }

        if (role.Name == AppRoles.Admin && !AppRoles.IsOwner(actor.Role.Name))
        {
            throw new BusinessException("Only Owner can assign the Admin role.");
        }

        return role;
    }

    private static void EnsureCanManageTarget(ActorInfo actor, User target)
    {
        if (AppRoles.IsOwner(target.Role.Name) && !AppRoles.IsOwner(actor.Role.Name))
        {
            throw new BusinessException("Only Owner can edit Owner users.");
        }

        if (target.Role.Name == AppRoles.Admin && !AppRoles.IsOwner(actor.Role.Name) && actor.Id != target.Id)
        {
            throw new BusinessException("Only Owner can edit other Admin users.");
        }
    }

    private sealed record ActorInfo(int Id, Role Role);
}
