using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.SettingsManage)]
public class SettingsController : Controller
{
    private readonly IStoreSettingsService _settings;
    private readonly IBackupService _backups;
    private readonly ILicenseService _licenses;
    private readonly IWarehouseService _warehouses;
    private readonly ITreasuryService _treasuries;
    private readonly IWebHostEnvironment _env;
    private readonly IStringLocalizer<SharedResource> _l;

    public SettingsController(
        IStoreSettingsService settings,
        IBackupService backups,
        ILicenseService licenses,
        IWarehouseService warehouses,
        ITreasuryService treasuries,
        IWebHostEnvironment env,
        IStringLocalizer<SharedResource> localizer)
    {
        _settings = settings;
        _backups = backups;
        _licenses = licenses;
        _warehouses = warehouses;
        _treasuries = treasuries;
        _env = env;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        return View(await BuildPageAsync(tab ?? "store", cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] StoreSettingsFormViewModel form,
        string tab,
        IFormFile? logoFile,
        CancellationToken cancellationToken)
    {
        // Each tab posts only its fields; merge with current settings before validating names etc.
        ModelState.Clear();

        try
        {
            var merged = await MergeTabAsync(tab, form, cancellationToken);
            if (logoFile is { Length: > 0 })
            {
                merged.LogoRelativePath = await SaveLogoAsync(logoFile, cancellationToken);
            }

            await _settings.SaveAsync(merged, cancellationToken);
            TempData["Success"] = _l["Settings saved."].Value;
            return RedirectToAction(nameof(Index), new { tab });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
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
            ModelState.AddModelError(string.Empty, ex.Message);
            var page = await BuildPageAsync("license", cancellationToken);
            page.LicenseForm = form;
            return View("Index", page);
        }
    }

    private async Task<SettingsPageViewModel> BuildPageAsync(string tab, CancellationToken cancellationToken)
    {
        var current = await _settings.GetAsync(cancellationToken);
        ViewData["Section"] = _l["Settings"].Value;
        ViewData["Title"] = _l["Settings"].Value;
        ViewData["Subtitle"] = _l["Store profile, printing, backup, and license."].Value;

        return new SettingsPageViewModel
        {
            Tab = tab,
            Form = new StoreSettingsFormViewModel
            {
                NameAr = current.NameAr,
                NameEn = current.NameEn,
                AddressAr = current.AddressAr,
                AddressEn = current.AddressEn,
                Phone = current.Phone,
                TaxNumber = current.TaxNumber,
                CommercialRegister = current.CommercialRegister,
                ReceiptFooterAr = current.ReceiptFooterAr,
                ReceiptFooterEn = current.ReceiptFooterEn,
                ReceiptLanguage = current.ReceiptLanguage,
                ReceiptDigits = current.ReceiptDigits,
                PricesIncludeVat = current.PricesIncludeVat,
                VatPercent = current.VatPercent,
                ReturnWindowDays = current.ReturnWindowDays,
                AllowSellAtZeroStock = current.AllowSellAtZeroStock,
                PaperWidthMm = current.PaperWidthMm,
                DefaultReceiptFormat = current.DefaultReceiptFormat,
                SellingWarehouseId = current.SellingWarehouseId,
                DefaultTreasuryId = current.DefaultTreasuryId,
                InvoiceNumberPrefix = current.InvoiceNumberPrefix,
                ReturnNumberPrefix = current.ReturnNumberPrefix,
                DefaultUiLanguage = current.DefaultUiLanguage,
                LogoRelativePath = current.LogoRelativePath,
                BackupFolder = current.BackupFolder,
                BackupOnShiftClose = current.BackupOnShiftClose,
                DailyBackupEnabled = current.DailyBackupEnabled,
                DailyBackupHourLocal = current.DailyBackupHourLocal
            },
            Warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken),
            Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken),
            Backups = await _backups.ListBackupsAsync(cancellationToken),
            License = await _licenses.GetStatusAsync(cancellationToken)
        };
    }

    private async Task<string> SaveLogoAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            throw new BusinessException("Logo must be PNG, JPG, or WebP.");
        }

        if (file.Length > 2_000_000)
        {
            throw new BusinessException("Logo file is too large (2 MB max).");
        }

        var relDir = Path.Combine("uploads");
        var absDir = Path.Combine(_env.WebRootPath, relDir);
        Directory.CreateDirectory(absDir);
        var fileName = "logo" + ext;
        var absPath = Path.Combine(absDir, fileName);
        await using var stream = System.IO.File.Create(absPath);
        await file.CopyToAsync(stream, cancellationToken);
        return Path.Combine(relDir, fileName).Replace('\\', '/');
    }

    private async Task<StoreSettingsFormDto> MergeTabAsync(
        string tab,
        StoreSettingsFormViewModel form,
        CancellationToken cancellationToken)
    {
        var current = await _settings.GetAsync(cancellationToken);
        var dto = new StoreSettingsFormDto
        {
            NameAr = current.NameAr,
            NameEn = current.NameEn,
            AddressAr = current.AddressAr,
            AddressEn = current.AddressEn,
            Phone = current.Phone,
            TaxNumber = current.TaxNumber,
            CommercialRegister = current.CommercialRegister,
            ReceiptFooterAr = current.ReceiptFooterAr,
            ReceiptFooterEn = current.ReceiptFooterEn,
            ReceiptLanguage = current.ReceiptLanguage,
            ReceiptDigits = current.ReceiptDigits,
            PricesIncludeVat = current.PricesIncludeVat,
            VatPercent = current.VatPercent,
            ReturnWindowDays = current.ReturnWindowDays,
            PaperWidthMm = current.PaperWidthMm,
            DefaultReceiptFormat = current.DefaultReceiptFormat,
            SellingWarehouseId = current.SellingWarehouseId,
            DefaultTreasuryId = current.DefaultTreasuryId,
            InvoiceNumberPrefix = current.InvoiceNumberPrefix,
            ReturnNumberPrefix = current.ReturnNumberPrefix,
            DefaultUiLanguage = current.DefaultUiLanguage,
            AllowSellAtZeroStock = current.AllowSellAtZeroStock,
            LogoRelativePath = current.LogoRelativePath,
            BackupFolder = current.BackupFolder,
            BackupOnShiftClose = current.BackupOnShiftClose,
            DailyBackupEnabled = current.DailyBackupEnabled,
            DailyBackupHourLocal = current.DailyBackupHourLocal
        };

        switch (tab)
        {
            case "store":
                dto.NameAr = form.NameAr;
                dto.NameEn = form.NameEn;
                dto.AddressAr = form.AddressAr;
                dto.AddressEn = form.AddressEn;
                dto.Phone = form.Phone;
                dto.TaxNumber = form.TaxNumber;
                dto.CommercialRegister = form.CommercialRegister;
                dto.SellingWarehouseId = form.SellingWarehouseId;
                dto.DefaultTreasuryId = form.DefaultTreasuryId;
                dto.LogoRelativePath = form.LogoRelativePath ?? current.LogoRelativePath;
                break;
            case "sales":
                dto.PricesIncludeVat = form.PricesIncludeVat;
                dto.VatPercent = form.VatPercent;
                dto.ReturnWindowDays = form.ReturnWindowDays;
                dto.AllowSellAtZeroStock = form.AllowSellAtZeroStock;
                break;
            case "numbering":
                dto.InvoiceNumberPrefix = form.InvoiceNumberPrefix;
                dto.ReturnNumberPrefix = form.ReturnNumberPrefix;
                break;
            case "printing":
                dto.PaperWidthMm = form.PaperWidthMm;
                dto.DefaultReceiptFormat = form.DefaultReceiptFormat;
                dto.ReceiptLanguage = form.ReceiptLanguage;
                dto.ReceiptDigits = form.ReceiptDigits;
                dto.ReceiptFooterAr = form.ReceiptFooterAr;
                dto.ReceiptFooterEn = form.ReceiptFooterEn;
                break;
            case "language":
                dto.DefaultUiLanguage = form.DefaultUiLanguage;
                break;
            case "backup":
                dto.BackupFolder = form.BackupFolder;
                dto.BackupOnShiftClose = form.BackupOnShiftClose;
                dto.DailyBackupEnabled = form.DailyBackupEnabled;
                dto.DailyBackupHourLocal = form.DailyBackupHourLocal;
                break;
        }

        return dto;
    }
}
