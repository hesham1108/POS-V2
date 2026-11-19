using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>Journeys A, B, C (gate) and H (close). See docs/page-composition.md §4–§6.</summary>
[Authorize(Policy = AppPermissions.ShiftsOpenResume)]
public class ShiftsController : Controller
{
    private const string ReturnToGate = "gate";

    private readonly IShiftService _shifts;
    private readonly IStringLocalizer<SharedResource> _l;

    public ShiftsController(IShiftService shifts, IStringLocalizer<SharedResource> localizer)
    {
        _shifts = shifts;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Gate(CancellationToken cancellationToken)
    {
        var gate = await _shifts.GetGateAsync(this.GetRequiredUserId(), cancellationToken);

        // B, already joined: nothing to decide — go straight to selling.
        if (gate.State == ShiftGateState.OpenShift && gate.IsParticipant)
        {
            return AfterGate();
        }

        return View(BuildGate(gate));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(ShiftGateViewModel model, CancellationToken cancellationToken)
    {
        var userId = this.GetRequiredUserId();
        if (!ModelState.IsValid)
        {
            return await GateViewWithErrorsAsync(userId, model.OpeningAmount, cancellationToken);
        }

        try
        {
            var shift = await _shifts.OpenAsync(model.OpeningAmount, userId, cancellationToken);
            TempData["Success"] = string.Format(_l["Shift #{0} opened."], shift.Id);
            return AfterGate();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l[ex.Message]);
            return await GateViewWithErrorsAsync(userId, model.OpeningAmount, cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _shifts.ResumeAsync(this.GetRequiredUserId(), cancellationToken);
            TempData["Success"] = string.Format(_l["Joined shift #{0}."], shift.Id);
            return AfterGate();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l[ex.Message].Value;
            return RedirectToAction(nameof(Gate));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
    {
        var summary = await _shifts.GetOpenShiftAsync(cancellationToken);
        if (summary is null)
        {
            return RedirectToAction(nameof(Gate));
        }

        var userId = this.GetRequiredUserId();
        return View(new ShiftCurrentViewModel
        {
            Summary = summary,
            IsParticipant = summary.Participants.Any(x => x.UserId == userId),
            CanClose = User.HasPermission(AppPermissions.ShiftsClose)
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.ShiftsClose)]
    public async Task<IActionResult> Close(string? returnTo, CancellationToken cancellationToken)
    {
        var summary = await _shifts.GetOpenShiftAsync(cancellationToken);
        if (summary is null)
        {
            TempData["Error"] = _l["There is no open shift."].Value;
            return RedirectToAction(nameof(Gate));
        }

        return View(new ShiftCloseViewModel
        {
            Summary = summary,
            ShiftId = summary.Id,
            ReturnTo = returnTo == ReturnToGate ? ReturnToGate : null
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ShiftsClose)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(ShiftCloseViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var closed = await _shifts.CloseAsync(new CloseShiftDto
                {
                    ShiftId = model.ShiftId,
                    ActualCash = model.ActualCash,
                    Notes = model.Notes
                }, this.GetRequiredUserId(), cancellationToken);

                var message = string.Format(_l["Shift #{0} closed."], closed.Id);

                // Journey C: old shift closed from the gate → back to the gate to open a new one.
                if (model.ReturnTo == ReturnToGate)
                {
                    TempData["Success"] = message;
                    return RedirectToAction(nameof(Gate));
                }

                // Journey H: confirm → print report (Phase 8) → back to login.
                // TODO(Phase 8): open Receipts/Shift/{id} for printing before signing out.
                TempData["Success"] = message;
                Response.Cookies.Delete(AuthConstants.AccessTokenCookieName);
                return RedirectToAction("Login", "Account");
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, _l[ex.Message]);
            }
        }

        var summary = await _shifts.GetOpenShiftAsync(cancellationToken);
        if (summary is null)
        {
            TempData["Error"] = _l["This shift is already closed."].Value;
            return RedirectToAction(nameof(Gate));
        }

        model.Summary = summary;
        model.ShiftId = summary.Id;
        return View(model);
    }

    /// <summary>Where the gate hands off. TODO(Phase 7): POS/Index once the POS screen exists.</summary>
    private IActionResult AfterGate() => RedirectToAction(nameof(Current));

    private ShiftGateViewModel BuildGate(ShiftGateDto gate, decimal? openingAmount = null) => new()
    {
        Gate = gate,
        CanClose = User.HasPermission(AppPermissions.ShiftsClose),
        OpeningAmount = openingAmount
    };

    private async Task<IActionResult> GateViewWithErrorsAsync(
        int userId,
        decimal? openingAmount,
        CancellationToken cancellationToken)
    {
        var gate = await _shifts.GetGateAsync(userId, cancellationToken);
        return View(nameof(Gate), BuildGate(gate, openingAmount));
    }
}
