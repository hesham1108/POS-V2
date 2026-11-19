using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IBackupService
{
    Task<BackupResultDto> BackupNowAsync(string? reason = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupFileDto>> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a .bak into the current database. Requires exclusive access — stop other users first.
    /// </summary>
    Task<BackupResultDto> RestoreAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Best-effort backup used after shift close / daily job; never throws.</summary>
    Task TryBackupAsync(string reason, CancellationToken cancellationToken = default);
}
