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

/// <summary>Journey E (docs/page-composition.md §10): quick cash-out modal (POS + current shift) and the list.</summary>
[Authorize(Policy = AppPolicies.CashOutsView)]
public class CashOutsController : Controller
{
    private readonly ICashOutService _cashOuts;
    private readonly IShiftService _shifts;
    private readonly IStringLocalizer<SharedResource> _l;

    public CashOutsController(ICashOutService cashOuts, IShiftService shifts, IStringLocalizer<SharedResource> localizer)
    {
        _cashOuts = cashOuts;
        _shifts = shifts;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from,
        DateTime? to,
        int? shiftId,
        int? userId,
        [FromQuery] ListQuery list,
        CancellationToken cancellationToken)
    {
        var canSeeAll = User.HasPermission(AppPermissions.ReportsView);
        var activeShift = await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken);

        PagedResult<CashOutListItemDto> page = new();
        IReadOnlyList<(int UserId, string DisplayName)> users = [];
        if (canSeeAll || activeShift.HasValue)
        {
            var filter = new CashOutFilterDto
            {
                // Dates are picked in store-local time; the end date is inclusive.
                FromUtc = canSeeAll ? from?.Date.ToUniversalTime() : null,
                ToUtc = canSeeAll ? to?.Date.AddDays(1).ToUniversalTime() : null,
                ShiftId = canSeeAll ? shiftId : activeShift
            };
            users = await _cashOuts.GetUsersAsync(filter, cancellationToken);
            filter.UserId = userId;
            page = await _cashOuts.GetPageAsync(filter, list.ForRequest(Request), cancellationToken);
        }

        if (Request.WantsCsv())
        {
            return CsvExport.File("cash-outs", page.Items,
                (_l["Date"].Value, x => x.CreatedAtUtc.ToLocalTime()),
                (_l["Shift"].Value, x => x.ShiftId),
                (_l["User"].Value, x => x.UserDisplayName),
                (_l["Amount"].Value, x => x.Amount),
                (_l["Reason"].Value, x => x.Reason));
        }

        return View(new CashOutIndexViewModel
        {
            From = canSeeAll ? from : null,
            To = canSeeAll ? to : null,
            ShiftId = canSeeAll ? shiftId : activeShift,
            UserId = userId,
            CanSeeAll = canSeeAll,
            CanCreate = User.HasPermission(AppPermissions.CashOutsCreate),
            InShift = activeShift.HasValue,
            Users = users.Select(u => (u.UserId, u.DisplayName)).ToList(),
            Items = page.Items,
            Page = page,
            Total = page.Sum ?? 0m
        });
    }

    /// <summary>AJAX from the cash-out modal: 200 JSON, or 400 + localized message.</summary>
    [HttpPost]
    [Authorize(Policy = AppPermissions.CashOutsCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(decimal? amount, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _cashOuts.CreateAsync(
                new CreateCashOutDto { Amount = amount, Reason = reason },
                this.GetRequiredUserId(),
                cancellationToken);

            var message = string.Format(
                _l["Cash-out of {0} EGP saved. Expected in drawer: {1} EGP."].Value,
                result.Amount.ToString("N2"),
                result.ExpectedCash.ToString("N2"));
            if (result.ExceededDrawer)
            {
                message += " " + _l["Warning: this is more than the drawer was expected to hold."].Value;
            }

            return Json(new
            {
                ok = true,
                message,
                warning = result.ExceededDrawer,
                expectedCash = result.ExpectedCash
            });
        }
        catch (BusinessException ex)
        {
            return BadRequest(_l.Localize(ex));
        }
    }
}
