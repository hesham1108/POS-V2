using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Printing;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Printable documents (80 mm): sale receipt and shift report. Opened in a hidden iframe with
/// <c>?autoprint=1</c> so the browser prints straight to the store's default (thermal) printer.
/// See docs/printing.md for the kiosk-printing setup.
/// </summary>
[Authorize]
public class ReceiptsController : Controller
{
    private readonly ISalesService _sales;
    private readonly IShiftService _shifts;
    private readonly IReturnService _returns;
    private readonly IPurchaseService _purchases;
    private readonly StoreSettings _store;
    private readonly TimeProvider _time;

    public ReceiptsController(
        ISalesService sales,
        IShiftService shifts,
        IReturnService returns,
        IPurchaseService purchases,
        IOptions<StoreSettings> store,
        TimeProvider time)
    {
        _sales = sales;
        _shifts = shifts;
        _returns = returns;
        _purchases = purchases;
        _store = store.Value;
        _time = time;
    }

    [HttpGet]
    public async Task<IActionResult> Sale(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        // Back office may print any invoice; a cashier only invoices of the shift they are working in.
        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = User.HasPermission(AppPermissions.PosUse)
                ? await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken)
                : null;
            if (activeShift != invoice.ShiftId)
            {
                return Forbid();
            }
        }

        return View(new SaleReceiptViewModel
        {
            Invoice = invoice,
            Format = new ReceiptFormat(_store),
            BarcodeSvg = Code128.ToSvg(invoice.InvoiceNumber),
            AutoPrint = autoprint,
            Returns = await _returns.GetListAsync(new SalesReturnFilterDto { InvoiceId = id }, cancellationToken)
        });
    }

    /// <summary>A4 landscape sales invoice for a regular printer (Phase 11).</summary>
    [HttpGet]
    public async Task<IActionResult> SaleA4(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = User.HasPermission(AppPermissions.PosUse)
                ? await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken)
                : null;
            if (activeShift != invoice.ShiftId)
            {
                return Forbid();
            }
        }

        return View(new SaleReceiptViewModel
        {
            Invoice = invoice,
            Format = new ReceiptFormat(_store),
            BarcodeSvg = Code128.ToSvg(invoice.InvoiceNumber),
            AutoPrint = autoprint,
            Returns = await _returns.GetListAsync(new SalesReturnFilterDto { InvoiceId = id }, cancellationToken)
        });
    }

    /// <summary>Return slip (80 mm) handed to the customer with the refund.</summary>
    [HttpGet]
    public async Task<IActionResult> Return(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var ret = await _returns.GetAsync(id, cancellationToken);
        if (ret is null)
        {
            return NotFound();
        }

        // Same rule as sale receipts: back office any, otherwise only returns of the user's current shift.
        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken);
            if (!User.HasPermission(AppPermissions.ReturnsCreate) || activeShift is null || activeShift != ret.ShiftId)
            {
                return Forbid();
            }
        }

        return View(new ReturnReceiptViewModel
        {
            Return = ret,
            Format = new ReceiptFormat(_store),
            BarcodeSvg = Code128.ToSvg(ret.ReturnNumber),
            AutoPrint = autoprint
        });
    }

    /// <summary>A4 landscape return slip.</summary>
    [HttpGet]
    public async Task<IActionResult> ReturnA4(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var ret = await _returns.GetAsync(id, cancellationToken);
        if (ret is null)
        {
            return NotFound();
        }

        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken);
            if (!User.HasPermission(AppPermissions.ReturnsCreate) || activeShift is null || activeShift != ret.ShiftId)
            {
                return Forbid();
            }
        }

        return View(new ReturnReceiptViewModel
        {
            Return = ret,
            Format = new ReceiptFormat(_store),
            BarcodeSvg = Code128.ToSvg(ret.ReturnNumber),
            AutoPrint = autoprint
        });
    }

    /// <summary>A4 landscape purchase invoice.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.PurchasesManage)]
    public async Task<IActionResult> PurchaseA4(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.GetByIdAsync(id, cancellationToken);
        if (purchase is null)
        {
            return NotFound();
        }

        return View(new PurchaseA4ViewModel
        {
            Purchase = purchase,
            Format = new ReceiptFormat(_store),
            AutoPrint = autoprint
        });
    }

    [HttpGet]
    public async Task<IActionResult> Shift(int id, bool autoprint, CancellationToken cancellationToken)
    {
        if (!User.HasPermission(AppPermissions.ReportsView) && !User.HasPermission(AppPermissions.ShiftsClose))
        {
            return Forbid();
        }

        var shift = await _shifts.GetSummaryAsync(id, cancellationToken);
        if (shift is null)
        {
            return NotFound();
        }

        return View(new ShiftReportViewModel
        {
            Shift = shift,
            Format = new ReceiptFormat(_store),
            PrintedAtUtc = _time.GetUtcNow().UtcDateTime,
            AutoPrint = autoprint
        });
    }
}
