using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public AuditLogService(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task AddAsync(
        int? actorUserId,
        string action,
        string entityType,
        int? entityId,
        int? targetUserId,
        string details,
        CancellationToken cancellationToken = default)
    {
        var actorName = actorUserId is null
            ? "system"
            : await _db.Users.AsNoTracking().Where(x => x.Id == actorUserId).Select(x => x.DisplayName).FirstOrDefaultAsync(cancellationToken) ?? "?";

        _db.AuditLogs.Add(new AuditLog
        {
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            ActorUserId = actorUserId,
            ActorName = actorName.Length > 100 ? actorName[..100] : actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TargetUserId = targetUserId,
            Details = details.Length > 2000 ? details[..2000] : details
        });
    }

    public async Task<IReadOnlyList<AuditEntryDto>> ListForUserAsync(int userId, int take = 100, CancellationToken cancellationToken = default) =>
        await _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.TargetUserId == userId || x.ActorUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new AuditEntryDto
            {
                CreatedAtUtc = x.CreatedAtUtc,
                ActorName = x.ActorName,
                Action = x.Action,
                Details = x.Details,
                IsAboutThisUser = x.TargetUserId == userId
            })
            .ToListAsync(cancellationToken);
}
