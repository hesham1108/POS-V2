using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>Journey G (docs/page-composition.md §12): find the invoice (scan the receipt barcode) → lines → refund.</summary>
[Authorize(Policy = AppPolicies.ReturnsView)]
public class ReturnsController : Controller
{
    private readonly IReturnService _returns;
    private readonly IShiftService _shifts;
    private readonly IStringLocalizer<SharedResource> _l;

    public ReturnsController(IReturnService returns, IShiftService shifts, IStringLocalizer<SharedResource> localizer)
    {
        _returns = returns;
        _shifts = shifts;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        DateTime? from,
        DateTime? to,
        PaymentMethod? method,
        int? printId,
        [FromQuery] ListQuery list,
        CancellationToken cancellationToken)
    {
        var canSeeAll = User.HasPermission(AppPermissions.ReportsView);
        search = list.Term ?? search;
        PagedResult<SalesReturnListItemDto> page = new();

        // Without Reports.View the list is the returns made in the shift the user is working in.
        var activeShift = canSeeAll
            ? null
            : await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken);
        if (canSeeAll || activeShift.HasValue)
        {
            page = await _returns.GetPageAsync(new SalesReturnFilterDto
            {
                Search = search,
                FromUtc = from?.Date.ToUniversalTime(),
                ToUtc = to?.Date.AddDays(1).ToUniversalTime(),
                RefundMethod = method,
                ShiftId = activeShift
            }, list.ForRequest(Request), cancellationToken);
        }

        if (Request.WantsCsv())
        {
            return CsvExport.File("returns", page.Items,
                (_l["Return"].Value, x => x.ReturnNumber),
                (_l["Invoice"].Value, x => x.OriginalInvoiceNumber),
                (_l["Date"].Value, x => x.CreatedAtUtc.ToLocalTime()),
                (_l["Shift"].Value, x => x.ShiftId),
                (_l["User"].Value, x => x.UserDisplayName),
                (_l["Refund method"].Value, x => x.RefundMethod.ToString()),
                (_l["Amount"].Value, x => x.TotalAmount),
                (_l["Reason"].Value, x => x.Reason));
        }

        return View(new ReturnIndexViewModel
        {
            Search = search,
            From = from,
            To = to,
            Method = method,
            PrintId = printId,
            CanSeeAll = canSeeAll,
            CanCreate = User.HasPermission(AppPermissions.ReturnsCreate),
            Items = page.Items,
            Page = page,
            Total = page.Sum ?? 0m
        });
    }

    /// <summary>Without <paramref name="saleId"/>: scan/type the invoice number. With it: the return form.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.ReturnsCreate)]
    public async Task<IActionResult> Create(int? saleId, string? number, CancellationToken cancellationToken)
    {
        if (!saleId.HasValue && !string.IsNullOrWhiteSpace(number))
        {
            saleId = await _returns.FindInvoiceIdAsync(number, cancellationToken);
            if (!saleId.HasValue)
            {
                ModelState.AddModelError(string.Empty, string.Format(_l["No completed invoice with number \"{0}\"."].Value, number.Trim()));
                return View(new ReturnCreateViewModel { Number = number });
            }

            return RedirectToAction(nameof(Create), new { saleId });
        }

        if (!saleId.HasValue)
        {
            return View(new ReturnCreateViewModel());
        }

        var invoice = await _returns.GetReturnableAsync(saleId.Value, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        return View(BuildForm(invoice, null));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ReturnsCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReturnCreateViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var saved = await _returns.CreateAsync(new CreateReturnDto
                {
                    InvoiceId = model.InvoiceId,
                    Lines = model.Lines
                        .Where(x => x.Quantity is > 0)
                        .Select(x => new ReturnLineInputDto { SalesInvoiceItemId = x.SalesInvoiceItemId, Quantity = x.Quantity!.Value })
                        .ToList(),
                    RefundMethod = model.RefundMethod,
                    Reason = model.Reason
                }, this.GetRequiredUserId(), cancellationToken);

                TempData["Success"] = saved.RefundMethod == PaymentMethod.Cash
                    ? string.Format(_l["Return {0} saved. Give the customer {1} EGP from the drawer."].Value, saved.ReturnNumber, saved.TotalAmount.ToString("N2"))
                    : string.Format(_l["Return {0} saved. Refund {1} EGP by the original payment method."].Value, saved.ReturnNumber, saved.TotalAmount.ToString("N2"));
                return RedirectToAction(nameof(Index), new { printId = saved.Id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, _l.Localize(ex));
            }
        }

        var invoice = await _returns.GetReturnableAsync(model.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        return View(BuildForm(invoice, model));
    }

    private static ReturnCreateViewModel BuildForm(ReturnableInvoiceDto invoice, ReturnCreateViewModel? posted)
    {
        var quantities = posted?.Lines.ToDictionary(x => x.SalesInvoiceItemId, x => x.Quantity) ?? [];
        return new ReturnCreateViewModel
        {
            Invoice = invoice,
            InvoiceId = invoice.InvoiceId,
            RefundMethod = posted?.RefundMethod ?? PaymentMethod.Cash,
            Reason = posted?.Reason,
            Lines = invoice.Lines
                .Select(x => new ReturnLineInput
                {
                    SalesInvoiceItemId = x.SalesInvoiceItemId,
                    Quantity = quantities.GetValueOrDefault(x.SalesInvoiceItemId)
                })
                .ToList()
        };
    }
}
