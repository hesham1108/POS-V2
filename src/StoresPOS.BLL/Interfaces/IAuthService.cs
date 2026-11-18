using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task EnsureSeedDataAsync(CancellationToken cancellationToken = default);
}
