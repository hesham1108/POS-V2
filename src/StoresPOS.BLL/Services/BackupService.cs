using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.Services;

public class BackupService : IBackupService
{
    private readonly IConfiguration _configuration;
    private readonly IStoreSettingsService _settings;
    private readonly ILogger<BackupService> _logger;
    private readonly TimeProvider _time;

    public BackupService(
        IConfiguration configuration,
        IStoreSettingsService settings,
        ILogger<BackupService> logger,
        TimeProvider time)
    {
        _configuration = configuration;
        _settings = settings;
        _logger = logger;
        _time = time;
    }

    public async Task<BackupResultDto> BackupNowAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        var folder = ResolveFolder();
        Directory.CreateDirectory(folder);

        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"StoresPOS_{stamp}.bak";
        var fullPath = Path.Combine(folder, fileName);
        var dbName = GetDatabaseName();

        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 600;
        cmd.CommandText = """
            BACKUP DATABASE [{0}]
            TO DISK = @path
            WITH FORMAT, INIT, NAME = @name, SKIP, NOREWIND, NOUNLOAD, STATS = 10;
            """;
        // Identifier cannot be parameterized; db name comes from our connection string only.
        cmd.CommandText = cmd.CommandText.Replace("[{0}]", "[" + dbName.Replace("]", "]]") + "]", StringComparison.Ordinal);
        cmd.Parameters.AddWithValue("@path", fullPath);
        cmd.Parameters.AddWithValue("@name", $"StoresPOS full {(reason ?? "manual")}");

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new BackupResultDto
        {
            Succeeded = true,
            FilePath = fullPath,
            Message = $"Backup saved to {fullPath}",
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };
    }

    public Task<IReadOnlyList<BackupFileDto>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var folder = ResolveFolder();
        if (!Directory.Exists(folder))
        {
            return Task.FromResult<IReadOnlyList<BackupFileDto>>([]);
        }

        var rows = Directory.EnumerateFiles(folder, "StoresPOS_*.bak")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new BackupFileDto
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length,
                    CreatedAtUtc = info.CreationTimeUtc
                };
            })
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToList();

        return Task.FromResult<IReadOnlyList<BackupFileDto>>(rows);
    }

    public async Task<BackupResultDto> RestoreAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.Contains("..", StringComparison.Ordinal)
            || !fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Invalid backup file name.");
        }

        var fullPath = Path.Combine(ResolveFolder(), fileName);
        if (!File.Exists(fullPath))
        {
            throw new BusinessException("Backup file not found.");
        }

        var dbName = GetDatabaseName();
        var masterCs = new SqlConnectionStringBuilder(GetConnectionString())
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using var conn = new SqlConnection(masterCs);
        await conn.OpenAsync(cancellationToken);

        // Kick users off, then restore with REPLACE.
        await using (var kick = conn.CreateCommand())
        {
            kick.CommandTimeout = 120;
            kick.CommandText = $"""
                ALTER DATABASE [{dbName.Replace("]", "]]")}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                """;
            await kick.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await using var restore = conn.CreateCommand();
            restore.CommandTimeout = 600;
            restore.CommandText = $"""
                RESTORE DATABASE [{dbName.Replace("]", "]]")}]
                FROM DISK = @path
                WITH REPLACE, RECOVERY;
                """;
            restore.Parameters.AddWithValue("@path", fullPath);
            await restore.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await using var multi = conn.CreateCommand();
            multi.CommandText = $"""
                ALTER DATABASE [{dbName.Replace("]", "]]")}] SET MULTI_USER;
                """;
            try
            {
                await multi.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not return database to MULTI_USER after restore.");
            }
        }

        return new BackupResultDto
        {
            Succeeded = true,
            FilePath = fullPath,
            Message = "Database restored. Restart the application.",
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };
    }

    public async Task TryBackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_settings.Current.BackupOnShiftClose && string.Equals(reason, "shift-close", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await BackupNowAsync(reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup ({Reason}) failed.", reason);
        }
    }

    private string ResolveFolder()
    {
        var folder = _settings.Current.BackupFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = @"C:\StoresPOS\Backups";
        }

        return folder.Trim();
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("DefaultConnection")
        ?? throw new BusinessException("Database connection string is missing.");

    private string GetDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(GetConnectionString());
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new BusinessException("Connection string has no database name.");
        }

        return builder.InitialCatalog;
    }
}
