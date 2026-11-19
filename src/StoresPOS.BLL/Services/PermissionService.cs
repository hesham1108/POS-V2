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

    public Task<bool> HasPermissionAsync(int userId, string permission, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .SelectMany(u => u.Role.Permissions)
            .AnyAsync(p => p.Permission == permission, cancellationToken);
}
