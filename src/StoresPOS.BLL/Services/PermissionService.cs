using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Services;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;

    public PermissionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permission, CancellationToken cancellationToken = default)
    {
        var perms = await GetEffectivePermissionsAsync(userId, cancellationToken);
        return perms.Contains(permission, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.RoleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return [];
        }

        var userPerms = await _db.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);

        if (userPerms.Count > 0)
        {
            return userPerms;
        }

        return await _db.RolePermissions
            .AsNoTracking()
            .Where(x => x.RoleId == user.RoleId)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);
    }
}
