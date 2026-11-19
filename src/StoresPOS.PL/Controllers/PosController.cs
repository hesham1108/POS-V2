using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Cashier screen (journey D). The page loads once; cart actions are AJAX POSTs that return the
/// re-rendered <c>_PosCart</c> partial, or 400 + a localized message, or 409 + X-Redirect to the gate.
/// </summary>
[Authorize(Policy = AppPermissions.PosUse)]
public class PosController : Controller
{
    private readonly ISalesService _sales;
    private readonly IShiftService _shifts;
    private readonly IStringLocalizer<SharedResource> _l;

    public PosController(ISalesService sales, IShiftService shifts, IStringLocalizer<SharedResource> localizer)
    {
        _sales = sales;
        _shifts = shifts;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = this.GetRequiredUserId();
        var shiftId = await _shifts.GetActiveShiftIdForUserAsync(userId, cancellationToken);
        if (shiftId is null)
        {
            return RedirectToAction("Gate", "Shifts");
        }

        var cart = await _sales.GetCartAsync(userId, cancellationToken);
        return View(new PosPageViewModel
        {
            Cart = cart,
            ShiftId = shiftId.Value,
            CashierName = User.FindFirst("display_name")?.Value ?? User.Identity?.Name ?? string.Empty,
            Categories = await _sales.GetPosCategoriesAsync(cancellationToken),
            Products = await _sales.SearchProductsAsync(new PosSearchDto { SaleType = cart.SaleType }, cancellationToken),
            CanOverridePrice = User.HasPermission(AppPermissions.SalesPriceOverride),
            CanDiscount = User.HasPermission(AppPermissions.SalesDiscount),
            CanCloseShift = User.HasPermission(AppPermissions.ShiftsClose),
            CanSeeDashboard = User.HasPermission(AppPermissions.DashboardView)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Products(string? term, int? categoryId, CancellationToken cancellationToken) =>
        await Run(async userId =>
        {
            var cart = await _sales.GetCartAsync(userId, cancellationToken);
            var products = await _sales.SearchProductsAsync(
                new PosSearchDto { Term = term, CategoryId = categoryId, SaleType = cart.SaleType },
                cancellationToken);
            return PartialView("_PosProducts", products);
        });

    [HttpGet]
    public Task<IActionResult> Cart(CancellationToken cancellationToken) =>
        CartAction(userId => _sales.GetCartAsync(userId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Scan(string code, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.ScanAsync(userId, code, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Add(int productId, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.AddProductAsync(userId, productId, 1m, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Quantity(int lineId, decimal? delta, decimal? quantity, CancellationToken cancellationToken) =>
        CartAction(userId => quantity.HasValue
            ? _sales.SetQuantityAsync(userId, lineId, quantity.Value, cancellationToken)
            : _sales.ChangeQuantityAsync(userId, lineId, delta ?? 0m, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(int lineId, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.RemoveLineAsync(userId, lineId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaleType(SaleType saleType, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.SetSaleTypeAsync(userId, saleType, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Price(int lineId, decimal price, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.OverridePriceAsync(userId, lineId, price, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Discount(decimal? amount, decimal? percent, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.SetDiscountAsync(
            userId, new DiscountDto { Amount = amount, Percent = percent }, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Clear(CancellationToken cancellationToken) =>
        CartAction(userId => _sales.ClearAsync(userId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Hold(CancellationToken cancellationToken) =>
        CartAction(userId => _sales.HoldAsync(userId, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Held(CancellationToken cancellationToken) =>
        await Run(async userId => PartialView("_PosHeld", await _sales.GetHeldAsync(userId, cancellationToken)));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Resume(int invoiceId, CancellationToken cancellationToken) =>
        CartAction(userId => _sales.ResumeHeldAsync(userId, invoiceId, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(
        PaymentMethod method,
        decimal? amountPaid,
        string? reference,
        CancellationToken cancellationToken) =>
        await Run(async userId =>
        {
            var sale = await _sales.CompleteAsync(
                userId,
                new PaymentDto { Method = method, AmountPaid = amountPaid, Reference = reference },
                cancellationToken);

            // TODO(Phase 8): send Receipts/Sale/{id} to the 80 mm printer here.
            var message = sale.PaymentMethod == PaymentMethod.Cash
                ? string.Format(_l["Payment received. Invoice {0}. Change: {1} EGP."].Value, sale.InvoiceNumber, sale.ChangeAmount.ToString("N2"))
                : string.Format(_l["Payment received. Invoice {0}."].Value, sale.InvoiceNumber);

            return Json(new { ok = true, invoiceId = sale.InvoiceId, invoiceNumber = sale.InvoiceNumber, message });
        });

    private Task<IActionResult> CartAction(Func<int, Task<CartDto>> action) =>
        Run(async userId => PartialView("_PosCart", new PosCartViewModel
        {
            Cart = await action(userId),
            CanOverridePrice = User.HasPermission(AppPermissions.SalesPriceOverride),
            CanDiscount = User.HasPermission(AppPermissions.SalesDiscount)
        }));

    private async Task<IActionResult> Run(Func<int, Task<IActionResult>> action)
    {
        try
        {
            return await action(this.GetRequiredUserId());
        }
        catch (BusinessException ex) when (ex.MessageKey == SalesService.MsgNotInShift)
        {
            // Shift closed (or user not joined) while the page was open → back to the gate.
            Response.Headers["X-Redirect"] = Url.Action("Gate", "Shifts");
            return StatusCode(StatusCodes.Status409Conflict, _l.Localize(ex));
        }
        catch (BusinessException ex)
        {
            return BadRequest(_l.Localize(ex));
        }
    }
}
