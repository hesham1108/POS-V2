using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleListItemDto>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int actorUserId, UserUpsertDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(int actorUserId, int userId, UserUpsertDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RolePermissionMatrixDto>> GetRoleMatricesAsync(CancellationToken cancellationToken = default);
    Task SaveRolePermissionsAsync(int actorUserId, int roleId, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);

    // Phase 21
    Task<IReadOnlyList<UserListItemDto>> ListAsync(UserListFilterDto filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntryDto>> GetActivityAsync(int userId, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int actorUserId, int userId, bool active, CancellationToken cancellationToken = default);
    Task SignOutEverywhereAsync(int actorUserId, int userId, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(int actorUserId, int userId, string newPassword, bool mustChange, CancellationToken cancellationToken = default);
    Task SetMustChangePasswordAsync(int actorUserId, int userId, bool mustChange, CancellationToken cancellationToken = default);
    Task UnlockAsync(int actorUserId, int userId, CancellationToken cancellationToken = default);
    Task SetApprovalPinAsync(int actorUserId, int userId, string? pin, CancellationToken cancellationToken = default);
    Task SaveRoleMatrixAsync(int actorUserId, IReadOnlyDictionary<int, IReadOnlyList<string>> rolePermissions, CancellationToken cancellationToken = default);
    Task<int> CreateRoleFromTemplateAsync(int actorUserId, string templateKey, string name, CancellationToken cancellationToken = default);
}
