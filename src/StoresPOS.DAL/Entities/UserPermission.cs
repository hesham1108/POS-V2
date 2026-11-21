namespace StoresPOS.DAL.Entities;

/// <summary>
/// Explicit per-user permission grant. When a user has any rows, these replace the role matrix for that user.
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public string Permission { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
