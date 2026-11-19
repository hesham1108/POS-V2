using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface ILicenseService
{
    string GetDeviceId();

    Task<LicenseStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<LicenseStatusDto> ActivateAsync(LicenseActivateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Builds the signature we put on issued license files (same secret as verification).</summary>
    string Sign(string deviceId, string licensedTo, DateTime? expiresUtc);
}
