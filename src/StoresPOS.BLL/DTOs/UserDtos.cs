namespace StoresPOS.BLL.DTOs;

public class UserListItemDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasCustomTabs { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int RoleId { get; set; }
    public bool IsLocked { get; set; }
    public bool MustChangePassword { get; set; }
}

public class UserListFilterDto
{
    public string? Search { get; set; }
    public int? RoleId { get; set; }

    /// <summary>null = all, true = active only, false = inactive only.</summary>
    public bool? IsActive { get; set; }
}

public class UserStampsDto
{
    public int PermissionStamp { get; set; }
    public int SecurityStamp { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
}

public class AuditEntryDto
{
    public DateTime CreatedAtUtc { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    /// <summary>True when the entry is a change made to this user (false = something this user did).</summary>
    public bool IsAboutThisUser { get; set; }
}

public class UserDetailsDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool UseCustomTabs { get; set; }
    public IReadOnlyList<string> EffectivePermissions { get; set; } = [];
    public IReadOnlyList<string> CustomPermissions { get; set; } = [];

    /// <summary>The role's own permissions — what "from role" means for each box.</summary>
    public IReadOnlyList<string> RolePermissions { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool HasApprovalPin { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}

public class UserUpsertDto
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool UseCustomTabs { get; set; }
    public IReadOnlyList<string> CustomPermissions { get; set; } = [];

    /// <summary>With a new or reset password: the user must choose their own at next sign-in.</summary>
    public bool MustChangePassword { get; set; }
}

public class RoleListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RolePermissionMatrixDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}
