using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Printing;
using StoresPOS.PL.ViewModels;

using StoresPOS.PL.Extensions;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.SettingsManage)]
public class SettingsController : Controller
{
    private readonly IStoreSettingsService _settings;
    private readonly IBackupService _backups;
    private readonly ILicenseService _licenses;
    private readonly IWarehouseService _warehouses;
    private readonly ITreasuryService _treasuries;
    private readonly IWindowsPrinterService _printers;
    private readonly ISilentPrintService _silentPrint;
    private readonly StoresPOS.BLL.Services.IPrintTemplateService _templates;
    private readonly IWebHostEnvironment _env;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IStoreSettingsService settings,
        IBackupService backups,
        ILicenseService licenses,
        IWarehouseService warehouses,
        ITreasuryService treasuries,
        IWindowsPrinterService printers,
        ISilentPrintService silentPrint,
        StoresPOS.BLL.Services.IPrintTemplateService templates,
        IWebHostEnvironment env,
        IStringLocalizer<SharedResource> localizer,
        ILogger<SettingsController> logger)
    {
        _settings = settings;
        _backups = backups;
        _licenses = licenses;
        _warehouses = warehouses;
        _treasuries = treasuries;
        _printers = printers;
        _silentPrint = silentPrint;
        _templates = templates;
        _env = env;
        _l = localizer;
        _logger = logger;
    }

    /// <summary>Tabs of the settings page. Each one loads only its own data (Phase 18).</summary>
    public static readonly string[] Tabs = ["store", "sales", "numbering", "appearance", "language", "printing", "templates", "backup", "license"];

    [HttpGet]
    public async Task<IActionResult> Index(string? tab, string? doc, string? format, CancellationToken cancellationToken)
    {
        var page = await BuildPageAsync(NormalizeTab(tab), cancellationToken);
        if (page.Tab == "templates")
        {
            (doc, format) = TemplateTarget(doc, format);
            page.Template = _templates.Get(doc, format, _settings.Current);
            page.TemplateCustomized = _templates.IsCustomized(doc, format);
        }

        return View(page);
    }

    /// <summary>Saves the designer's template (JSON from templates.js).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(string template, CancellationToken cancellationToken)
    {
        PrintTemplate? t = null;
        try
        {
            t = System.Text.Json.JsonSerializer.Deserialize<PrintTemplate>(template ?? "", new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException)
        {
        }

        if (t is null)
        {
            return BadRequest();
        }

        try
        {
            await _templates.SaveAsync(t, cancellationToken);
            TempData["Success"] = _l["Template saved."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index), new { tab = "templates", doc = t.Document, format = PrintTemplateCatalog.NormalizeFormat(t.Format) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetTemplate(string doc, string format, CancellationToken cancellationToken)
    {
        (doc, format) = TemplateTarget(doc, format);
        await _templates.ResetAsync(doc, format, cancellationToken);
        TempData["Success"] = _l["Template reset to the default layout."].Value;
        return RedirectToAction(nameof(Index), new { tab = "templates", doc, format });
    }

    private static (string Doc, string Format) TemplateTarget(string? doc, string? format)
    {
        var f = PrintTemplateCatalog.NormalizeFormat(format);
        var d = doc ?? PrintTemplateCatalog.Sale;
        return PrintTemplateCatalog.Exists(d, f) ? (d, f)
            : PrintTemplateCatalog.Exists(d, PrintTemplateCatalog.A4) ? (d, PrintTemplateCatalog.A4)
            : (PrintTemplateCatalog.Sale, PrintTemplateCatalog.Mm80);
    }

    /// <summary>Printer list for the Printing tab, loaded with AJAX after the page (cached 60 s; refresh=true asks Windows again).</summary>
    [HttpGet]
    public IActionResult Printers(bool refresh = false)
    {
        if (refresh)
        {
            _printers.Invalidate();
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var list = BuildPrinterList(_settings.Current);
        _logger.LogInformation("Settings printers list: {Ms} ms (refresh {Refresh})", watch.ElapsedMilliseconds, refresh);
        return Json(list);
    }

    private static string NormalizeTab(string? tab) =>
        tab is not null && Tabs.Contains(tab) ? tab : "store";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TestSilentPrint(string? format, string? printerName)
    {
        var result = _silentPrint.PrintSample(format, printerName);
        return Json(new
        {
            ok = result.Succeeded,
            printed = result.Succeeded,
            printerName = result.PrinterName,
            message = result.Message
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultPrinter(string? printerName, CancellationToken cancellationToken)
    {
        var merged = ToDto(await _settings.GetAsync(cancellationToken));
        merged.DefaultPrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();
        await _settings.SaveAsync(merged, cancellationToken);

        var applied = false;
        if (!string.IsNullOrWhiteSpace(merged.DefaultPrinterName))
        {
            applied = _printers.SetWindowsDefault(merged.DefaultPrinterName);
        }

        return Json(new
        {
            ok = true,
            windowsApplied = applied,
            printers = BuildPrinterList(_settings.Current)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] StoreSettingsFormViewModel form,
        string tab,
        IFormFile? logoFile,
        IFormFile? uiLogoFile,
        IFormFile? faviconFile,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        tab = NormalizeTab(tab);

        try
        {
            var merged = await MergeTabAsync(tab, form, cancellationToken);
            if (logoFile is { Length: > 0 })
            {
                merged.LogoRelativePath = await SaveUploadAsync(logoFile, "logo", cancellationToken);
            }

            if (uiLogoFile is { Length: > 0 })
            {
                merged.UiLogoRelativePath = await SaveUploadAsync(uiLogoFile, "ui-logo", cancellationToken);
            }

            if (faviconFile is { Length: > 0 })
            {
                merged.FaviconRelativePath = await SaveUploadAsync(faviconFile, "favicon", cancellationToken, allowIco: true);
            }

            await _settings.SaveAsync(merged, cancellationToken);

            if (tab == "printing" && !string.IsNullOrWhiteSpace(merged.DefaultPrinterName))
            {
                _printers.SetWindowsDefault(merged.DefaultPrinterName);
            }

            TempData["Success"] = _l["Settings saved."].Value;
            return RedirectToAction(nameof(Index), new { tab });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            var page = await BuildPageAsync(tab, cancellationToken);
            page.Form = form;
            return View("Index", page);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackupNow(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _backups.BackupNowAsync("manual", cancellationToken);
            TempData["Success"] = result.Message;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "backup" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _backups.RestoreAsync(fileName, cancellationToken);
            TempData["Success"] = result.Message;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "backup" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateLicense(
        [Bind(Prefix = "LicenseForm")] LicenseActivateViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var page = await BuildPageAsync("license", cancellationToken);
            page.LicenseForm = form;
            return View("Index", page);
        }

        try
        {
            await _licenses.ActivateAsync(new LicenseActivateDto
            {
                LicensedTo = form.LicensedTo,
                ExpiresUtc = form.ExpiresUtc,
                Signature = form.Signature,
                DeviceId = _licenses.GetDeviceId()
            }, cancellationToken);
            TempData["Success"] = _l["License activated."].Value;
            return RedirectToAction(nameof(Index), new { tab = "license" });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            var page = await BuildPageAsync("license", cancellationToken);
            page.LicenseForm = form;
            return View("Index", page);
        }
    }

    private PrinterListDto BuildPrinterList(StoreSettings current) => new()
    {
        IsSupported = _printers.IsSupported,
        WindowsDefault = _printers.GetWindowsDefault(),
        StoredDefault = current.DefaultPrinterName,
        ThermalPrinter = current.ThermalPrinterName,
        A4Printer = current.A4PrinterName,
        Printers = _printers.ListPrinters(),
        Message = _printers.IsSupported
            ? null
            : _l["Printer listing is available on Windows hosts only."].Value
    };

    /// <summary>
    /// Loads what the selected tab shows — and only that (Phase 18). Before, every tab loaded
    /// warehouses, treasuries, the backup folder, the license and the Windows printer list
    /// (67 s on the first open). The printer list is no longer loaded here at all: the Printing
    /// tab fetches it with AJAX (<see cref="Printers"/>). Each stage is still timed and logged
    /// ("Settings page timing ...") so before/after numbers can be compared.
    /// </summary>
    private async Task<SettingsPageViewModel> BuildPageAsync(string tab, CancellationToken cancellationToken)
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        var step = System.Diagnostics.Stopwatch.StartNew();
        long Lap()
        {
            var ms = step.ElapsedMilliseconds;
            step.Restart();
            return ms;
        }

        var current = await _settings.GetAsync(cancellationToken);
        var settingsMs = Lap();
        ViewData["Section"] = _l["Settings"].Value;
        ViewData["Title"] = _l["Settings"].Value;
        ViewData["Subtitle"] = _l["Store profile, printing, backup, and license."].Value;

        var page = new SettingsPageViewModel { Tab = tab, Form = ToForm(current) };
        long warehousesMs = 0, treasuriesMs = 0, backupsMs = 0, licenseMs = 0;

        switch (tab)
        {
            case "store":
                page.Warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
                warehousesMs = Lap();
                page.Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
                treasuriesMs = Lap();
                break;
            case "backup":
                page.Backups = await _backups.ListBackupsAsync(cancellationToken);
                backupsMs = Lap();
                break;
            case "license":
                page.License = await _licenses.GetStatusAsync(cancellationToken);
                licenseMs = Lap();
                break;
        }

        _logger.LogInformation(
            "Settings page timing (tab {Tab}): settings {SettingsMs} ms, warehouses {WarehousesMs} ms, treasuries {TreasuriesMs} ms, "
            + "backups {BackupsMs} ms, license {LicenseMs} ms, printers (AJAX) 0 ms, total {TotalMs} ms",
            tab, settingsMs, warehousesMs, treasuriesMs, backupsMs, licenseMs, total.ElapsedMilliseconds);

        return page;
    }

    private async Task<string> SaveUploadAsync(
        IFormFile file,
        string baseName,
        CancellationToken cancellationToken,
        bool allowIco = false)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = allowIco
            ? new[] { ".png", ".jpg", ".jpeg", ".webp", ".ico" }
            : new[] { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowed.Contains(ext))
        {
            throw new BusinessException(allowIco
                ? "File must be PNG, JPG, WebP, or ICO."
                : "Logo must be PNG, JPG, or WebP.");
        }

        if (file.Length > 2_000_000)
        {
            throw new BusinessException("File is too large (2 MB max).");
        }

        var relDir = Path.Combine("uploads");
        var absDir = Path.Combine(_env.WebRootPath, relDir);
        Directory.CreateDirectory(absDir);
        var fileName = baseName + ext;
        var absPath = Path.Combine(absDir, fileName);
        await using var stream = System.IO.File.Create(absPath);
        await file.CopyToAsync(stream, cancellationToken);
        return Path.Combine(relDir, fileName).Replace('\\', '/');
    }

    /// <summary>
    /// Current settings + the fields of the saved tab (fields marked <see cref="SettingsTabAttribute"/>).
    /// Empty upload paths keep the stored file.
    /// </summary>
    private async Task<StoreSettingsFormDto> MergeTabAsync(
        string tab,
        StoreSettingsFormViewModel form,
        CancellationToken cancellationToken)
    {
        var current = await _settings.GetAsync(cancellationToken);
        var dto = ToDto(current);
        MergeTab(tab, form, dto);
        return dto;
    }

    public static void MergeTab(string tab, StoreSettingsFormViewModel form, StoreSettingsFormDto dto)
    {
        foreach (var source in typeof(StoreSettingsFormViewModel).GetProperties())
        {
            var tabs = source.GetCustomAttributes(typeof(SettingsTabAttribute), inherit: false)
                .Cast<SettingsTabAttribute>()
                .SelectMany(a => a.Tabs);
            if (!tabs.Contains(tab))
            {
                continue;
            }

            var target = typeof(StoreSettingsFormDto).GetProperty(source.Name)
                ?? throw new InvalidOperationException($"StoreSettingsFormDto has no property {source.Name}.");
            var value = source.GetValue(form);
            if (source.Name.EndsWith("RelativePath", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(value as string))
            {
                continue;
            }

            target.SetValue(dto, value is DocumentPrintSettings doc ? SettingsMapper.Map<DocumentPrintSettings>(doc) : value);
        }
    }

    public static StoreSettingsFormViewModel ToForm(StoreSettings current) =>
        SettingsMapper.Map<StoreSettingsFormViewModel>(current);

    public static StoreSettingsFormDto ToDto(StoreSettings current) =>
        SettingsMapper.Map<StoreSettingsFormDto>(current);
}
