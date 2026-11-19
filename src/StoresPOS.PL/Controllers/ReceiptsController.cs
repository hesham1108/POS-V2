using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
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
    private readonly StoreSettings _store;
    private readonly TimeProvider _time;

    public ReceiptsController(
        ISalesService sales,
        IShiftService shifts,
        IOptions<StoreSettings> store,
        TimeProvider time)
    {
        _sales = sales;
        _shifts = shifts;
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
