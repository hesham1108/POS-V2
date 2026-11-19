namespace StoresPOS.DAL.Entities;

/// <summary>One granted permission for a role. Permission keys live in BLL <c>AppPermissions</c>.</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
}
