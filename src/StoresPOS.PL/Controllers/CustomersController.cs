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

/// <summary>
/// Customers (docs/page-composition.md §18). Management screens need Reports.View (Admin/Manager);
/// the POS picker (F4) uses <see cref="PosController"/> with Pos.Use.
/// </summary>
[Authorize(Policy = AppPermissions.ReportsView)]
public class CustomersController : Controller
{
    private readonly ICustomerService _customers;
    private readonly ISalesService _sales;
    private readonly IReturnService _returns;
    private readonly IChequeService _cheques;
    private readonly IStringLocalizer<SharedResource> _l;

    public CustomersController(
        ICustomerService customers,
        ISalesService sales,
        IReturnService returns,
        IChequeService cheques,
        IStringLocalizer<SharedResource> localizer)
    {
        _customers = customers;
        _sales = sales;
        _returns = returns;
        _cheques = cheques;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _customers.GetListAsync(
            new CustomerListFilterDto { Search = search, IncludeInactive = includeInactive },
            cancellationToken);

        return View(new CustomerIndexViewModel { Search = search, IncludeInactive = includeInactive, Customers = list });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerDetailsViewModel
        {
            Customer = customer,
            Invoices = await _sales.GetInvoicesAsync(new SalesInvoiceFilterDto { CustomerId = id, Take = 200 }, cancellationToken),
            Returns = await _returns.GetListAsync(new SalesReturnFilterDto { CustomerId = id, Take = 200 }, cancellationToken),
            Cheques = await _cheques.ListForCustomerAsync(id, cancellationToken),
            OutstandingCheques = await _cheques.OutstandingTotalForCustomerAsync(id, cancellationToken)
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var created = await _customers.CreateAsync(ToUpsert(model), cancellationToken);
            TempData["Success"] = string.Format(_l["Customer {0} added."].Value, created.Name);
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var c = await _customers.GetByIdAsync(id, cancellationToken);
        if (c is null)
        {
            return NotFound();
        }

        return View(new CustomerFormViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Address = c.Address,
            Notes = c.Notes,
            DefaultSaleType = c.DefaultSaleType,
            IsActive = c.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _customers.UpdateAsync(id, ToUpsert(model), cancellationToken);
            TempData["Success"] = _l["Customer updated."].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _customers.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Customer deactivated. Past invoices keep the name."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l.Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    private static CustomerUpsertDto ToUpsert(CustomerFormViewModel model) => new()
    {
        Name = model.Name,
        Phone = model.Phone,
        Address = model.Address,
        Notes = model.Notes,
        DefaultSaleType = model.DefaultSaleType,
        IsActive = model.IsActive
    };
}
