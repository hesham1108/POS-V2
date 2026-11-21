namespace StoresPOS.DAL.Entities;

/// <summary>
/// Who changed what (Phase 21): users, roles, permissions, passwords (never the value), sign-outs, lockouts.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>User who did it; null for the system (e.g. automatic lockout).</summary>
    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Short key, e.g. "user.created", "user.permissions", "role.permissions", "user.password".</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }

    /// <summary>The user affected (for the user's Activity tab).</summary>
    public int? TargetUserId { get; set; }

    /// <summary>Readable summary of the change (no secrets).</summary>
    public string Details { get; set; } = string.Empty;
}
