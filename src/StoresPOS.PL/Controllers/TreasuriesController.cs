using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.TreasuriesView)]
public class TreasuriesController : Controller
{
    private readonly ITreasuryService _treasuries;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly TimeProvider _time;

    public TreasuriesController(
        ITreasuryService treasuries,
        IStringLocalizer<SharedResource> localizer,
        TimeProvider time)
    {
        _treasuries = treasuries;
        _l = localizer;
        _time = time;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeInactive, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Treasuries"].Value;
        ViewData["Subtitle"] = _l["Balances and ledger for every cash and bank location."].Value;
        return View(new TreasuryIndexViewModel
        {
            IncludeInactive = includeInactive,
            Treasuries = await _treasuries.ListAsync(includeInactive, cancellationToken),
            CanManage = User.HasPermission(AppPermissions.TreasuriesManage),
            CanMove = User.HasPermission(AppPermissions.TreasuriesMove)
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.TreasuriesManage)]
    public IActionResult Create()
    {
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Add treasury"].Value;
        return View("Edit", new TreasuryEditViewModel { IsActive = true, Type = TreasuryType.Cash });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.TreasuriesManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var row = await _treasuries.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Edit treasury"].Value;
        return View(new TreasuryEditViewModel
        {
            Id = row.Id,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Type = row.Type,
            OpeningBalance = row.OpeningBalance,
            IsActive = row.IsActive,
            IsDefault = row.IsDefault
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.TreasuriesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TreasuryEditViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = model.Id.HasValue ? _l["Edit treasury"].Value : _l["Add treasury"].Value;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var form = new TreasuryFormDto
            {
                Id = model.Id,
                NameEn = model.NameEn,
                NameAr = model.NameAr,
                Type = model.Type,
                OpeningBalance = model.OpeningBalance,
                IsActive = model.IsActive,
                IsDefault = model.IsDefault
            };
            if (model.Id.HasValue)
            {
                await _treasuries.UpdateAsync(form, cancellationToken);
            }
            else
            {
                await _treasuries.CreateAsync(form, cancellationToken);
            }

            TempData["Success"] = _l["Saved."].Value;
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.TreasuriesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _treasuries.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Deactivated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l.Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.TreasuriesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _treasuries.ActivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Activated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l.Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.TreasuriesMove)]
    public async Task<IActionResult> Deposit(int id, CancellationToken cancellationToken)
    {
        var row = await _treasuries.GetAsync(id, cancellationToken);
        if (row is null || !row.IsActive)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Deposit"].Value;
        return View("Move", new TreasuryMoveViewModel
        {
            TreasuryId = row.Id,
            TreasuryName = row.NameEn,
            Balance = row.Balance,
            Mode = "deposit"
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.TreasuriesMove)]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        var row = await _treasuries.GetAsync(id, cancellationToken);
        if (row is null || !row.IsActive)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Withdraw"].Value;
        return View("Move", new TreasuryMoveViewModel
        {
            TreasuryId = row.Id,
            TreasuryName = row.NameEn,
            Balance = row.Balance,
            Mode = "withdraw"
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.TreasuriesMove)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(TreasuryMoveViewModel model, CancellationToken cancellationToken)
    {
        var isDeposit = string.Equals(model.Mode, "deposit", StringComparison.OrdinalIgnoreCase);
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = isDeposit ? _l["Deposit"].Value : _l["Withdraw"].Value;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var dto = new TreasuryMoveDto
            {
                TreasuryId = model.TreasuryId,
                Amount = model.Amount,
                Reason = model.Reason
            };
            if (isDeposit)
            {
                await _treasuries.DepositAsync(dto, this.GetRequiredUserId(), cancellationToken);
            }
            else
            {
                await _treasuries.WithdrawAsync(dto, this.GetRequiredUserId(), cancellationToken);
            }

            TempData["Success"] = _l["Saved."].Value;
            return RedirectToAction(nameof(Statement), new { id = model.TreasuryId });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.TreasuriesMove)]
    public async Task<IActionResult> Transfer(CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Transfer"].Value;
        var list = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
        return View(new TreasuryTransferViewModel { Treasuries = list });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.TreasuriesMove)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(TreasuryTransferViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Transfer"].Value;
        model.Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _treasuries.TransferAsync(new TreasuryTransferDto
            {
                FromTreasuryId = model.FromTreasuryId,
                ToTreasuryId = model.ToTreasuryId,
                Amount = model.Amount,
                Note = model.Note
            }, this.GetRequiredUserId(), cancellationToken);

            TempData["Success"] = _l["Transfer saved."].Value;
            return RedirectToAction(nameof(Statement), new { id = model.ToTreasuryId });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Statement(
        int id,
        DateTime? from,
        DateTime? to,
        bool print,
        CancellationToken cancellationToken)
    {
        var zone = _time.LocalTimeZone;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        if (from.HasValue)
        {
            fromUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Unspecified), zone);
        }

        if (to.HasValue)
        {
            toUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Unspecified), zone);
        }

        TreasuryStatementDto statement;
        try
        {
            statement = await _treasuries.GetStatementAsync(new TreasuryStatementFilterDto
            {
                TreasuryId = id,
                FromUtc = fromUtc,
                ToUtc = toUtc
            }, cancellationToken);
        }
        catch (BusinessException)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Treasuries"].Value;
        ViewData["Title"] = _l["Treasury statement"].Value;
        ViewData["Subtitle"] = statement.Treasury.NameEn;
        if (print)
        {
            ViewData["PrintOnLoad"] = true;
        }

        return View(new TreasuryStatementViewModel
        {
            Statement = statement,
            FromDate = from?.Date,
            ToDate = to?.Date,
            Print = print
        });
    }
}
