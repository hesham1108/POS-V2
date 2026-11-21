namespace StoresPOS.BLL.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Effective permissions: user-specific set when any <c>UserPermissions</c> exist; otherwise role matrix.
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(int userId, CancellationToken cancellationToken = default);
}
