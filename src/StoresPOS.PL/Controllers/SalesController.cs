using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>Completed sales invoices (docs/page-composition.md §11): list, details, reprint, linked returns.</summary>
[Authorize(Policy = AppPermissions.ReportsView)]
public class SalesController : Controller
{
    private readonly ISalesService _sales;
    private readonly IReturnService _returns;

    public SalesController(ISalesService sales, IReturnService returns)
    {
        _sales = sales;
        _returns = returns;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        DateTime? from,
        DateTime? to,
        PaymentMethod? method,
        int? shiftId,
        CancellationToken cancellationToken)
    {
        // Dates are picked in store-local time; the end date is inclusive.
        var invoices = await _sales.GetInvoicesAsync(new SalesInvoiceFilterDto
        {
            Search = search,
            FromUtc = from?.Date.ToUniversalTime(),
            ToUtc = to?.Date.AddDays(1).ToUniversalTime(),
            PaymentMethod = method,
            ShiftId = shiftId
        }, cancellationToken);

        return View(new SalesIndexViewModel
        {
            Search = search,
            From = from,
            To = to,
            Method = method,
            ShiftId = shiftId,
            Invoices = invoices
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        ViewData["Returns"] = await _returns.GetListAsync(new SalesReturnFilterDto { InvoiceId = id }, cancellationToken);
        return View(invoice);
    }
}
