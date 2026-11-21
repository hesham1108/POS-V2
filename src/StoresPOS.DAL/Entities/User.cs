namespace StoresPOS.DAL.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>Bumped when the user's permissions change; a token with an older value is re-issued silently (Phase 21).</summary>
    public int PermissionStamp { get; set; } = 1;

    /// <summary>Bumped by "sign out everywhere", password reset and deactivation; older tokens are rejected.</summary>
    public int SecurityStamp { get; set; } = 1;

    /// <summary>Wrong passwords in a row; the account locks for a while after <c>MaxFailedLogins</c>.</summary>
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }

    /// <summary>Must pick a new password at the next sign-in (set by an admin reset).</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Hashed 4–6 digit PIN used to approve actions at the till (e.g. returns, price overrides).</summary>
    public string? ApprovalPinHash { get; set; }
    public string? ApprovalPinSalt { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}
