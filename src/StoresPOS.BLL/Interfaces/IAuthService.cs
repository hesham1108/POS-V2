using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task EnsureSeedDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Fresh token with current permissions; fails when the user is inactive or locked (Phase 21).</summary>
    Task<AuthResultDto> RefreshAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserStampsDto?> GetStampsAsync(int userId, CancellationToken cancellationToken = default);

    Task<AuthResultDto> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
