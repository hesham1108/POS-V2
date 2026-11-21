using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.ViewModels;

public class UsersIndexViewModel
{
    public IReadOnlyList<UserListItemDto> Users { get; set; } = [];
    public IReadOnlyList<RoleListItemDto> Roles { get; set; } = [];
    public string? Search { get; set; }
    public int? RoleId { get; set; }

    /// <summary>"active", "inactive" or empty for all.</summary>
    public string? Status { get; set; }
}

public class UserEditViewModel
{
    public int? Id { get; set; }

    /// <summary>details | permissions | security | activity</summary>
    public string Tab { get; set; } = "details";

    [Required, MaxLength(64), Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(120), Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Must change password at next sign-in")]
    public bool MustChangePassword { get; set; }

    [Display(Name = "Role")]
    public int RoleId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Custom tabs for this user")]
    public bool UseCustomTabs { get; set; }

    public List<string> SelectedPermissions { get; set; } = [];

    public IReadOnlyList<RoleListItemDto> Roles { get; set; } = [];
    public string? RoleName { get; set; }

    /// <summary>Each role's permissions, so the page can show "from role" vs "custom" and reset to the role.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> RolePermissions { get; set; } = new Dictionary<int, IReadOnlyList<string>>();

    public UserDetailsDto? Details { get; set; }
    public IReadOnlyList<AuditEntryDto> Activity { get; set; } = [];
    public bool IsSelf { get; set; }
}

public class RoleMatrixPageViewModel
{
    public IReadOnlyList<RolePermissionMatrixDto> Roles { get; set; } = [];
    public int? SelectedRoleId { get; set; }
    public List<string> SelectedPermissions { get; set; } = [];
}
