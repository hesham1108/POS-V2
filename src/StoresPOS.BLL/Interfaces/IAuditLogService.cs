using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

/// <summary>Audit trail of user, role and permission changes (Phase 21).</summary>
public interface IAuditLogService
{
    /// <summary>Adds an entry to the change tracker; it is saved with the caller's next SaveChanges.</summary>
    Task AddAsync(int? actorUserId, string action, string entityType, int? entityId, int? targetUserId, string details, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntryDto>> ListForUserAsync(int userId, int take = 100, CancellationToken cancellationToken = default);
}
