using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>Journeys A, B, C (gate) and H (close). See docs/page-composition.md §4–§6.</summary>
[Authorize(Policy = AppPermissions.ShiftsOpenResume)]
public class ShiftsController : Controller
{
    private const string ReturnToGate = "gate";

    private readonly IShiftService _shifts;
    private readonly ISalesService _sales;
    private readonly ICashOutService _cashOuts;
    private readonly IReturnService _returns;
    private readonly IReportService _reports;
    private readonly ITreasuryService _treasuries;
    private readonly IStringLocalizer<SharedResource> _l;

    public ShiftsController(
        IShiftService shifts,
        ISalesService sales,
        ICashOutService cashOuts,
        IReturnService returns,
        IReportService reports,
        ITreasuryService treasuries,
        IStringLocalizer<SharedResource> localizer)
    {
        _shifts = shifts;
        _sales = sales;
        _cashOuts = cashOuts;
        _returns = returns;
        _reports = reports;
        _treasuries = treasuries;
        _l = localizer;
    }

    /// <summary>Shift history (docs/page-composition.md §6.2). Dates are store-local; the end date is inclusive.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.ReportsView)]
    public async Task<IActionResult> Index(
        DateTime? from,
        DateTime? to,
        int? userId,
        ShiftResultFilter? result,
        CancellationToken cancellationToken)
    {
        var shifts = await _shifts.GetHistoryAsync(new ShiftHistoryFilterDto
        {
            FromUtc = from?.Date.ToUniversalTime(),
            ToUtc = to?.Date.AddDays(1).ToUniversalTime(),
            UserId = userId,
            Result = result,
            Take = 500
        }, cancellationToken);

        return View(new ShiftHistoryViewModel
        {
            From = from,
            To = to,
            UserId = userId,
            Result = result,
            Shifts = shifts,
            Users = (await _reports.GetUserOptionsAsync(cancellationToken))
                .Select(x => new ReportOption { Id = x.Id ?? 0, Name = x.Name })
                .ToList()
        });
    }

    /// <summary>Read-only shift (docs/page-composition.md §6.3): drawer, sales by method and cashier, cash-outs, returns, invoices.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.ReportsView)]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var summary = await _shifts.GetSummaryAsync(id, cancellationToken);
        if (summary is null)
        {
            return NotFound();
        }

        return View(new ShiftDetailsViewModel
        {
            Summary = summary,
            Invoices = await _sales.GetInvoicesAsync(new SalesInvoiceFilterDto { ShiftId = id, Take = 1000 }, cancellationToken),
            CashOuts = await _cashOuts.GetListAsync(new CashOutFilterDto { ShiftId = id }, cancellationToken),
            Returns = await _returns.GetListAsync(new SalesReturnFilterDto { ShiftId = id }, cancellationToken)
        });
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
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
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
            TempData["Error"] = _l.Localize(ex);
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
        var isParticipant = summary.Participants.Any(x => x.UserId == userId);
        return View(new ShiftCurrentViewModel
        {
            Summary = summary,
            IsParticipant = isParticipant,
            CanClose = User.HasPermission(AppPermissions.ShiftsClose),
            // Cash-outs attach to the user's shift, so only participants can record one.
            CanCashOut = isParticipant && User.HasPermission(AppPermissions.CashOutsCreate),
            CashOuts = await _cashOuts.GetListAsync(new CashOutFilterDto { ShiftId = summary.Id }, cancellationToken)
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

        var treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
        var defaultTreasury = treasuries.FirstOrDefault(x => x.IsDefault) ?? treasuries.FirstOrDefault();
        return View(new ShiftCloseViewModel
        {
            Summary = summary,
            ShiftId = summary.Id,
            ReturnTo = returnTo == ReturnToGate ? ReturnToGate : null,
            Treasuries = treasuries,
            HandoverTreasuryId = defaultTreasury?.Id ?? 0
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
                    Notes = model.Notes,
                    HandoverTreasuryId = model.HandoverTreasuryId
                }, this.GetRequiredUserId(), cancellationToken);

                // Journey H: confirm → print report → login. Journey C: → print report → gate (open a new shift).
                return RedirectToAction(nameof(Closed), new { id = closed.Id, returnTo = model.ReturnTo, print = true });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, _l.Localize(ex));
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
        model.Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
        return View(model);
    }

    /// <summary>After close: prints the shift report and offers the next step (sign out, or open a new shift).</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.ShiftsClose)]
    public async Task<IActionResult> Closed(int id, string? returnTo, bool print, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetSummaryAsync(id, cancellationToken);
        if (shift is null || shift.Status != DAL.Entities.ShiftStatus.Closed)
        {
            return RedirectToAction(nameof(Gate));
        }

        return View(new ShiftClosedViewModel
        {
            Shift = shift,
            ReturnToGate = returnTo == ReturnToGate,
            AutoPrint = print
        });
    }

    /// <summary>
    /// Lets the closer hold another cashier's in-progress cart so the close can go ahead.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPermissions.ShiftsClose)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoldInProgress(int invoiceId, string? returnTo, CancellationToken cancellationToken)
    {
        try
        {
            await _sales.HoldForCashierAsync(this.GetRequiredUserId(), invoiceId, cancellationToken);
            TempData["Success"] = _l["Invoice held. It will be cancelled when the shift closes."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l.Localize(ex);
        }

        return RedirectToAction(nameof(Close), new { returnTo });
    }

    /// <summary>Where the gate hands off: the till for sellers, otherwise the shift page.</summary>
    private IActionResult AfterGate() =>
        User.HasPermission(AppPermissions.PosUse)
            ? RedirectToAction("Index", "Pos")
            : RedirectToAction(nameof(Current));

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
