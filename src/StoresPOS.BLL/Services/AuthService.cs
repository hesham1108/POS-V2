using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AdminSeedSettings _adminSeed;
    private readonly IAuditLogService? _audit;
    private readonly TimeProvider _time;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<AdminSeedSettings> adminSeedOptions,
        IAuditLogService? audit = null,
        TimeProvider? time = null)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _adminSeed = adminSeedOptions.Value;
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<AuthResultDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResultDto.Fail("Username and password are required.");
        }

        var username = request.Username.Trim();
        var user = await _db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AuthResultDto.Fail("Invalid username or password.");
        }

        // Phase 21: lock after repeated wrong passwords.
        if (user.LockedUntilUtc is { } lockedUntil && lockedUntil > Now)
        {
            return LockedResult(lockedUntil);
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= AuthClaims.MaxFailedLogins)
            {
                user.FailedLoginCount = 0;
                user.LockedUntilUtc = Now.Add(AuthClaims.LockoutDuration);
                if (_audit is not null)
                {
                    await _audit.AddAsync(null, "user.locked", "User", user.Id, user.Id,
                        $"Locked for {AuthClaims.LockoutDuration.TotalMinutes:0} minutes after {AuthClaims.MaxFailedLogins} wrong passwords.", cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
                return LockedResult(user.LockedUntilUtc.Value);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return AuthResultDto.Fail("Invalid username or password.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = Now;
        var result = await IssueAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// New token with the user's current permissions (Phase 21): used when the permission stamp in the
    /// token is older than the database one, so changes apply without signing in again.
    /// </summary>
    public async Task<AuthResultDto> RefreshAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || (user.LockedUntilUtc is { } until && until > Now))
        {
            return AuthResultDto.Fail("Your session has ended. Please sign in again.");
        }

        return await IssueAsync(user, cancellationToken);
    }

    public async Task<UserStampsDto?> GetStampsAsync(int userId, CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserStampsDto
            {
                PermissionStamp = x.PermissionStamp,
                SecurityStamp = x.SecurityStamp,
                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AuthResultDto> ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return AuthResultDto.Fail("Your session has ended. Please sign in again.");
        }

        if (!_passwordHasher.VerifyPassword(currentPassword ?? string.Empty, user.PasswordHash, user.PasswordSalt))
        {
            return AuthResultDto.Fail("Current password is wrong.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return AuthResultDto.Fail("Password must be at least 8 characters.");
        }

        if (_passwordHasher.VerifyPassword(newPassword, user.PasswordHash, user.PasswordSalt))
        {
            return AuthResultDto.Fail("Choose a password different from the current one.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;
        if (_audit is not null)
        {
            await _audit.AddAsync(user.Id, "user.password", "User", user.Id, user.Id, "Changed own password.", cancellationToken);
        }

        var result = await IssueAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<AuthResultDto> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var permissions = await LoadPermissionsAsync(user, cancellationToken);
        var (token, expiresAt) = _jwtTokenService.CreateToken(user, permissions);
        return new AuthResultDto
        {
            Succeeded = true,
            AccessToken = token,
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role.Name,
            MustChangePassword = user.MustChangePassword
        };
    }

    private async Task<List<string>> LoadPermissionsAsync(User user, CancellationToken cancellationToken)
    {
        var userPerms = await _db.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);

        return userPerms.Count > 0
            ? userPerms
            : await _db.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == user.RoleId)
                .Select(x => x.Permission)
                .ToListAsync(cancellationToken);
    }

    private AuthResultDto LockedResult(DateTime lockedUntilUtc)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((lockedUntilUtc - Now).TotalMinutes));
        var result = AuthResultDto.Fail("Too many wrong passwords. Try again in {0} minutes.");
        result.LockedMinutes = minutes;
        return result;
    }

    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in AppRoles.All)
        {
            var exists = await _db.Roles.AnyAsync(x => x.Name == roleName, cancellationToken);
            if (!exists)
            {
                _db.Roles.Add(new Role
                {
                    Name = roleName,
                    Description = $"{roleName} role"
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await EnsureRolePermissionsAsync(cancellationToken);
        await EnsureOwnerUserAsync(cancellationToken);

        if (await _db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_adminSeed.Password) || _adminSeed.Password.Length < 8)
        {
            throw new InvalidOperationException(
                "AdminSeed:Password must be configured and at least 8 characters for first-run setup.");
        }

        var ownerRole = await _db.Roles
            .SingleAsync(x => x.Name == AppRoles.Owner, cancellationToken);

        var (hash, salt) = _passwordHasher.HashPassword(_adminSeed.Password);
        _db.Users.Add(new User
        {
            Username = string.IsNullOrWhiteSpace(_adminSeed.Username) ? "admin" : _adminSeed.Username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(_adminSeed.DisplayName) ? "Store Owner" : _adminSeed.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleId = ownerRole.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the default permission matrix for roles that have none yet, and always keeps Owner/Admin
    /// at the full set (prevents lock-out and picks up permissions added in later versions).
    /// </summary>
    private async Task EnsureRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .Include(x => x.Permissions)
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var keepFull = role.Name is AppRoles.Owner or AppRoles.Admin;
            if (role.Permissions.Count > 0 && !keepFull)
            {
                continue;
            }

            var existing = role.Permissions.Select(x => x.Permission).ToHashSet(StringComparer.Ordinal);
            var added = false;
            foreach (var permission in AppPermissions.DefaultsFor(role.Name))
            {
                if (existing.Add(permission))
                {
                    _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
                    added = true;
                }
            }

            // A permission added in a new version (e.g. Profit.View) reaches signed-in users without a new sign-in.
            if (added && role.Permissions.Count > 0)
            {
                await _db.Users.Where(u => u.RoleId == role.Id).ForEachAsync(u => u.PermissionStamp++, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Promote the first Admin user to Owner when no Owner user exists yet.</summary>
    private async Task EnsureOwnerUserAsync(CancellationToken cancellationToken)
    {
        var ownerRole = await _db.Roles.SingleAsync(x => x.Name == AppRoles.Owner, cancellationToken);
        var hasOwnerUser = await _db.Users.AnyAsync(x => x.RoleId == ownerRole.Id, cancellationToken);
        if (hasOwnerUser)
        {
            return;
        }

        var adminRole = await _db.Roles.SingleOrDefaultAsync(x => x.Name == AppRoles.Admin, cancellationToken);
        if (adminRole is null)
        {
            return;
        }

        var firstAdmin = await _db.Users
            .Where(x => x.RoleId == adminRole.Id && x.IsActive)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstAdmin is null)
        {
            return;
        }

        firstAdmin.RoleId = ownerRole.Id;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
