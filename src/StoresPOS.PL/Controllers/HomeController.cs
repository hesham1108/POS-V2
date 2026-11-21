using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.Models;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Dashboard (Phase 22). Key figures and alerts render with the page; charts (JSON) and the
/// analysis/money/activity rows (HTML partial) load right after. Each part is cached per period (Dashboard:CacheSeconds, default 45 s).
/// Cashiers without Dashboard.View go to the shift gate.
/// </summary>
[Authorize]
public class HomeController : Controller
{

    private readonly IDashboardService _dashboard;
    private readonly IBackupService _backups;
    private readonly ILicenseService _licenses;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly TimeSpan _cacheFor;

    public HomeController(
        IDashboardService dashboard,
        IBackupService backups,
        ILicenseService licenses,
        IMemoryCache cache,
        TimeProvider time,
        IConfiguration configuration)
    {
        // Dashboard:CacheSeconds (default 45; 0 = no cache).
        _cacheFor = TimeSpan.FromSeconds(Math.Max(0, configuration.GetValue("Dashboard:CacheSeconds", 45)));
        _dashboard = dashboard;
        _backups = backups;
        _licenses = licenses;
        _cache = cache;
        _time = time;
    }

    public static DashboardPeriod ParsePeriod(string? period) => period switch
    {
        "yesterday" => DashboardPeriod.Yesterday,
        "7" => DashboardPeriod.Last7Days,
        "30" => DashboardPeriod.Last30Days,
        "month" => DashboardPeriod.ThisMonth,
        _ => DashboardPeriod.Today
    };

    public static string PeriodKey(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Yesterday => "yesterday",
        DashboardPeriod.Last7Days => "7",
        DashboardPeriod.Last30Days => "30",
        DashboardPeriod.ThisMonth => "month",
        _ => "today"
    };

    public async Task<IActionResult> Index(string? period, CancellationToken cancellationToken)
    {
        // Cashiers (no back-office access) always pass through the shift gate (journeys A/B/C).
        if (!User.HasPermission(AppPermissions.DashboardView))
        {
            return RedirectToAction("Gate", "Shifts");
        }

        var p = ParsePeriod(period);
        var overview = await _cache.GetOrCreateAsync("dash:overview:" + PeriodKey(p), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheFor > TimeSpan.Zero ? _cacheFor : TimeSpan.FromTicks(1);
            var dto = await _dashboard.GetOverviewAsync(p, cancellationToken);
            await FillSystemAlertsAsync(dto.Alerts, cancellationToken);
            return dto;
        });

        return View(overview!);
    }

    /// <summary>Sales by hour/day (with the previous period) and payment-method split, as JSON for Chart.js.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.DashboardView)]
    public async Task<IActionResult> Charts(string? period, CancellationToken cancellationToken)
    {
        var p = ParsePeriod(period);
        var charts = await _cache.GetOrCreateAsync("dash:charts:" + PeriodKey(p), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheFor > TimeSpan.Zero ? _cacheFor : TimeSpan.FromTicks(1);
            return _dashboard.GetChartsAsync(p, cancellationToken);
        });
        return Json(charts);
    }

    /// <summary>Top products, cashiers, running low, treasuries, cheques due, activity (HTML partial).</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.DashboardView)]
    public async Task<IActionResult> Details(string? period, CancellationToken cancellationToken)
    {
        var p = ParsePeriod(period);
        var details = await _cache.GetOrCreateAsync("dash:details:" + PeriodKey(p), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheFor > TimeSpan.Zero ? _cacheFor : TimeSpan.FromTicks(1);
            return _dashboard.GetDetailsAsync(p, cancellationToken);
        });
        return PartialView("_DashboardDetails", details!);
    }

    [Authorize(Policy = AppPermissions.DashboardView)]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>Backup age and license end date (both read files, so they stay in the cached overview).</summary>
    private async Task FillSystemAlertsAsync(DashboardAlertsDto alerts, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        try
        {
            var newest = (await _backups.ListBackupsAsync(cancellationToken)).MaxBy(x => x.CreatedAtUtc);
            alerts.HoursSinceBackup = newest is null ? null : Math.Round((now - newest.CreatedAtUtc).TotalHours, 1);
        }
        catch
        {
            alerts.HoursSinceBackup = null; // folder missing / not reachable → shown as "no recent backup"
        }

        try
        {
            var license = await _licenses.GetStatusAsync(cancellationToken);
            alerts.LicenseDaysLeft = license.ExpiresUtc is { } end ? (int)Math.Floor((end - now).TotalDays) : null;
        }
        catch
        {
            alerts.LicenseDaysLeft = null;
        }
    }
}
