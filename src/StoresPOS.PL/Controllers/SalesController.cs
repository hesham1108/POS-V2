using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

/// <summary>Completed sales invoices (docs/page-composition.md §11): list, details, reprint, linked returns.</summary>
[Authorize(Policy = AppPermissions.ReportsView)]
public class SalesController : Controller
{
    private readonly ISalesService _sales;
    private readonly IReturnService _returns;
    private readonly IStringLocalizer<SharedResource> _l;

    public SalesController(ISalesService sales, IReturnService returns, IStringLocalizer<SharedResource> localizer)
    {
        _sales = sales;
        _returns = returns;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        DateTime? from,
        DateTime? to,
        PaymentMethod? method,
        int? shiftId,
        int? cashierId,
        int? productId,
        [FromQuery] ListQuery list,
        CancellationToken cancellationToken)
    {
        // Dates are picked in store-local time; the end date is inclusive. "search" is the older name of "q".
        var term = list.Term ?? search;
        var page = await _sales.GetInvoicePageAsync(new SalesInvoiceFilterDto
        {
            Search = term,
            FromUtc = from?.Date.ToUniversalTime(),
            ToUtc = to?.Date.AddDays(1).ToUniversalTime(),
            PaymentMethod = method,
            ShiftId = shiftId,
            CashierUserId = cashierId,
            ProductId = productId
        }, list.ForRequest(Request), cancellationToken);

        if (Request.WantsCsv())
        {
            return CsvExport.File("sales-invoices", page.Items,
                (_l["Invoice"].Value, x => x.InvoiceNumber),
                (_l["Date"].Value, x => x.CompletedAtUtc.ToLocalTime()),
                (_l["Shift"].Value, x => x.ShiftId),
                (_l["Cashier"].Value, x => x.CashierDisplayName),
                (_l["Customer"].Value, x => x.CustomerName ?? _l["Walk-in customer"].Value),
                (_l["Sale type"].Value, x => x.SaleType == SaleType.Wholesale ? _l["Wholesale"].Value : _l["Retail"].Value),
                (_l["Payment method"].Value, x => x.PaymentMethod.ToString()),
                (_l["Items"].Value, x => x.LineCount),
                (_l["Total"].Value, x => x.Total),
                (_l["Returned"].Value, x => x.ReturnedAmount));
        }

        return View(new SalesIndexViewModel
        {
            Search = term,
            From = from,
            To = to,
            Method = method,
            ShiftId = shiftId,
            CashierId = cashierId,
            ProductId = productId,
            Page = page
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
