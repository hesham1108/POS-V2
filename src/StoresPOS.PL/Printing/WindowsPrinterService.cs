using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StoresPOS.PL.Printing;

/// <summary>
/// Windows printer list + default printer. Phase 18: asking Windows is slow (measured 67 s on the
/// first call on the dev PC with network printers), so the list and the default are read together
/// once and kept for <see cref="CacheFor"/>. Concurrent callers wait for the same read instead of
/// starting their own. <see cref="Invalidate"/> is used by the "Refresh" button and after changes.
/// </summary>
public sealed class WindowsPrinterService : IWindowsPrinterService
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDefaultPrinter(string printerName);

    private readonly object _gate = new();
    private readonly object _readGate = new();
    private readonly ILogger<WindowsPrinterService>? _logger;
    private Snapshot? _snapshot;
    private int _refreshing;

    public WindowsPrinterService(ILogger<WindowsPrinterService>? logger = null)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public IReadOnlyList<string> ListPrinters() => Read().Printers;

    public string? GetWindowsDefault() => Read().WindowsDefault;

    public void Invalidate()
    {
        lock (_gate)
        {
            _snapshot = null;
        }
    }

    public bool SetWindowsDefault(string printerName)
    {
        if (!IsSupported || string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        var name = printerName.Trim();
        if (!ListPrinters().Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var current = GetWindowsDefault();
        if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var ok = OperatingSystem.IsWindows() && SetDefaultPrinter(name);
            if (ok)
            {
                lock (_gate)
                {
                    if (_snapshot is not null)
                    {
                        _snapshot = _snapshot with { WindowsDefault = name };
                    }
                }
            }

            return ok;
        }
        catch
        {
            return false;
        }
    }

    private Snapshot Read()
    {
        Snapshot? stale;
        lock (_gate)
        {
            stale = _snapshot;
            if (stale is not null && DateTime.UtcNow - stale.ReadAtUtc < CacheFor)
            {
                return stale;
            }
        }

        if (stale is not null)
        {
            // Expired: serve the old list now and refresh in the background (one refresh at a time).
            if (Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var fresh = ReadNow();
                        lock (_gate)
                        {
                            _snapshot = fresh;
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _refreshing, 0);
                    }
                });
            }

            return stale;
        }

        lock (_readGate)
        {
            lock (_gate)
            {
                if (_snapshot is not null)
                {
                    return _snapshot;
                }
            }

            var fresh = ReadNow();
            lock (_gate)
            {
                _snapshot = fresh;
            }

            return fresh;
        }
    }

    private Snapshot ReadNow() =>
        IsSupported && OperatingSystem.IsWindows() ? ReadFromWindows() : Snapshot.Empty;

    [SupportedOSPlatform("windows")]
    private Snapshot ReadFromWindows()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var list = new List<string>();
        string? windowsDefault = null;
        try
        {
            foreach (string name in PrinterSettings.InstalledPrinters)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(name);
                }
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            list.Clear();
        }

        try
        {
            var name = new PrinterSettings().PrinterName;
            windowsDefault = string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            windowsDefault = null;
        }

        _logger?.LogInformation("Windows printers read: {Count} printer(s) in {Ms} ms", list.Count, watch.ElapsedMilliseconds);
        return new Snapshot(list, windowsDefault, DateTime.UtcNow);
    }

    private sealed record Snapshot(IReadOnlyList<string> Printers, string? WindowsDefault, DateTime ReadAtUtc)
    {
        public static Snapshot Empty => new([], null, DateTime.UtcNow);
    }
}

/// <summary>Reads the printer list once in the background at start-up so the first Settings open does not wait for Windows.</summary>
public sealed class PrinterWarmupHostedService : BackgroundService
{
    private readonly IWindowsPrinterService _printers;

    public PrinterWarmupHostedService(IWindowsPrinterService printers)
    {
        _printers = printers;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() =>
        {
            if (_printers.IsSupported)
            {
                _printers.ListPrinters();
            }
        }, stoppingToken);
}
