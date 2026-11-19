using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.Services;

public class LicenseService : ILicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly StoreSettingsPersistenceOptions _paths;
    private readonly LicenseOptions _licenseOptions;
    private readonly TimeProvider _time;

    public LicenseService(
        IOptions<StoreSettingsPersistenceOptions> paths,
        IOptions<LicenseOptions> licenseOptions,
        TimeProvider time)
    {
        _paths = paths.Value;
        _licenseOptions = licenseOptions.Value;
        _time = time;
    }

    public string GetDeviceId()
    {
        var raw = string.Join('|',
            Environment.MachineName,
            Environment.UserName,
            Environment.OSVersion.VersionString,
            Environment.ProcessorCount.ToString());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    public async Task<LicenseStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = GetDeviceId();
        var file = _paths.LicenseFilePath;
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            return new LicenseStatusDto
            {
                DeviceId = deviceId,
                IsActivated = false,
                StatusMessage = "No license file. Activate with a license issued for this device."
            };
        }

        try
        {
            await using var stream = File.OpenRead(file);
            var dto = await JsonSerializer.DeserializeAsync<LicenseActivateDto>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("empty");
            return Evaluate(dto, deviceId);
        }
        catch
        {
            return new LicenseStatusDto
            {
                DeviceId = deviceId,
                IsActivated = false,
                StatusMessage = "License file is invalid or corrupt."
            };
        }
    }

    public async Task<LicenseStatusDto> ActivateAsync(LicenseActivateDto dto, CancellationToken cancellationToken = default)
    {
        var deviceId = GetDeviceId();
        if (string.IsNullOrWhiteSpace(dto.LicensedTo) || string.IsNullOrWhiteSpace(dto.Signature))
        {
            throw new BusinessException("Licensed-to name and signature are required.");
        }

        if (!string.IsNullOrWhiteSpace(dto.DeviceId)
            && !string.Equals(dto.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("This license is bound to a different device.");
        }

        dto.DeviceId = deviceId;
        var status = Evaluate(dto, deviceId);
        if (!status.IsActivated)
        {
            throw new BusinessException(status.StatusMessage);
        }

        var dir = Path.GetDirectoryName(_paths.LicenseFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(
            _paths.LicenseFilePath,
            JsonSerializer.Serialize(dto, JsonOptions),
            cancellationToken);

        return status;
    }

    public string Sign(string deviceId, string licensedTo, DateTime? expiresUtc)
    {
        var payload = BuildPayload(deviceId, licensedTo, expiresUtc);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_licenseOptions.IssuerSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private LicenseStatusDto Evaluate(LicenseActivateDto dto, string deviceId)
    {
        var expected = Sign(deviceId, dto.LicensedTo.Trim(), dto.ExpiresUtc);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected.ToUpperInvariant()),
                Encoding.UTF8.GetBytes((dto.Signature ?? string.Empty).Trim().ToUpperInvariant())))
        {
            return new LicenseStatusDto
            {
                DeviceId = deviceId,
                IsActivated = false,
                StatusMessage = "License signature is not valid for this device."
            };
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var expired = dto.ExpiresUtc.HasValue && dto.ExpiresUtc.Value.Date < now.Date;
        return new LicenseStatusDto
        {
            DeviceId = deviceId,
            IsActivated = !expired,
            LicensedTo = dto.LicensedTo.Trim(),
            ExpiresUtc = dto.ExpiresUtc,
            IsExpired = expired,
            StatusMessage = expired
                ? "License expired. Request a renewed license file."
                : "License active."
        };
    }

    private static string BuildPayload(string deviceId, string licensedTo, DateTime? expiresUtc)
    {
        var exp = expiresUtc.HasValue
            ? expiresUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd")
            : "never";
        return $"{deviceId.Trim().ToUpperInvariant()}|{licensedTo.Trim()}|{exp}";
    }
}
