using StoresPOS.BLL.Interfaces;

namespace StoresPOS.PL.Hosting;

/// <summary>Runs one automatic backup per local day at the hour configured in Settings.</summary>
public sealed class DailyBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DailyBackupHostedService> _logger;
    private DateOnly? _lastBackupDay;

    public DailyBackupHostedService(IServiceScopeFactory scopes, ILogger<DailyBackupHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Daily backup tick failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IStoreSettingsService>();
        var current = settings.Current;
        if (!current.DailyBackupEnabled)
        {
            return;
        }

        var localNow = DateTime.Now;
        var today = DateOnly.FromDateTime(localNow);
        if (_lastBackupDay == today || localNow.Hour != current.DailyBackupHourLocal)
        {
            return;
        }

        var backups = scope.ServiceProvider.GetRequiredService<IBackupService>();
        await backups.TryBackupAsync("daily", cancellationToken);
        _lastBackupDay = today;
        _logger.LogInformation("Daily backup triggered for {Day}.", today);
    }
}
