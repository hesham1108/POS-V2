namespace StoresPOS.BLL.Interfaces;

/// <summary>Server-side permission checks from the database (not the token) for sensitive business actions.</summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permission, CancellationToken cancellationToken = default);
}
