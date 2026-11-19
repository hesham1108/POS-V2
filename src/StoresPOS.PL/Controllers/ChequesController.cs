using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.ChequesView)]
public class ChequesController : Controller
{
    private readonly IChequeService _cheques;
    private readonly ICustomerService _customers;
    private readonly ISupplierService _suppliers;
    private readonly ITreasuryService _treasuries;
    private readonly IStringLocalizer<SharedResource> _l;

    public ChequesController(
        IChequeService cheques,
        ICustomerService customers,
        ISupplierService suppliers,
        ITreasuryService treasuries,
        IStringLocalizer<SharedResource> localizer)
    {
        _cheques = cheques;
        _customers = customers;
        _suppliers = suppliers;
        _treasuries = treasuries;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        ChequeDirection? direction,
        ChequeStatus? status,
        DateTime? dueFrom,
        DateTime? dueTo,
        bool overdueOnly,
        string? search,
        bool print,
        CancellationToken cancellationToken)
    {
        var dir = direction ?? ChequeDirection.In;
        ViewData["Section"] = _l["Cheques"].Value;
        ViewData["Title"] = dir == ChequeDirection.In ? _l["Receivable (لينا)"].Value : _l["Payable (علينا)"].Value;
        ViewData["Subtitle"] = _l["Track due dates, collect or pay into a treasury."].Value;
        if (print)
        {
            ViewData["PrintOnLoad"] = true;
        }

        var result = await _cheques.ListAsync(new ChequeFilterDto
        {
            Direction = dir,
            Status = status,
            DueFrom = dueFrom,
            DueTo = dueTo,
            OverdueOnly = overdueOnly,
            Search = search
        }, cancellationToken);

        return View(new ChequeIndexViewModel
        {
            Direction = dir,
            Status = status,
            DueFrom = dueFrom,
            DueTo = dueTo,
            OverdueOnly = overdueOnly,
            Search = search,
            Result = result,
            CanManage = User.HasPermission(AppPermissions.ChequesManage),
            CanChangeStatus = User.HasPermission(AppPermissions.ChequesChangeStatus),
            Print = print
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.ChequesManage)]
    public async Task<IActionResult> Create(ChequeDirection? direction, int? customerId, int? supplierId, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Cheques"].Value;
        ViewData["Title"] = _l["Add cheque"].Value;
        var model = new ChequeEditViewModel
        {
            Direction = direction ?? (supplierId.HasValue ? ChequeDirection.Out : ChequeDirection.In),
            CustomerId = customerId,
            SupplierId = supplierId
        };
        await PopulatePartiesAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ChequesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChequeEditViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Cheques"].Value;
        ViewData["Title"] = _l["Add cheque"].Value;
        await PopulatePartiesAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var created = await _cheques.CreateAsync(new ChequeFormDto
            {
                Number = model.Number,
                Bank = model.Bank,
                DueDate = model.DueDate,
                Amount = model.Amount,
                Direction = model.Direction,
                CustomerId = model.CustomerId,
                SupplierId = model.SupplierId,
                Notes = model.Notes
            }, this.GetRequiredUserId(), cancellationToken);

            TempData["Success"] = _l["Saved."].Value;
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, _l.Localize(ex));
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var cheque = await _cheques.GetAsync(id, cancellationToken);
        if (cheque is null)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Cheques"].Value;
        ViewData["Title"] = string.Format(_l["Cheque {0}"].Value, cheque.Number);
        return View(new ChequeDetailsViewModel
        {
            Cheque = cheque,
            CanChangeStatus = User.HasPermission(AppPermissions.ChequesChangeStatus)
                              && !_cheques.IsTerminal(cheque.Status),
            Treasuries = await _treasuries.ListAsync(includeInactive: false, cancellationToken)
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ChequesChangeStatus)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(ChequeStatusFormViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _cheques.ChangeStatusAsync(new ChequeStatusChangeDto
            {
                ChequeId = model.ChequeId,
                NewStatus = model.NewStatus,
                TreasuryId = model.TreasuryId,
                Note = model.Note
            }, this.GetRequiredUserId(), cancellationToken);
            TempData["Success"] = _l["Status updated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = _l.Localize(ex);
        }

        return RedirectToAction(nameof(Details), new { id = model.ChequeId });
    }

    private async Task PopulatePartiesAsync(ChequeEditViewModel model, CancellationToken cancellationToken)
    {
        var customers = await _customers.GetListAsync(new CustomerListFilterDto { Take = 500 }, cancellationToken);
        model.CustomerOptions = customers
            .Select(x => new SelectOptionViewModel { Id = x.Id, Name = x.Name })
            .ToList();
        var suppliers = await _suppliers.GetListAsync(new SupplierListFilterDto { IsActive = true }, cancellationToken);
        model.SupplierOptions = suppliers
            .Select(x => new SelectOptionViewModel { Id = x.Id, Name = x.NameEn })
            .ToList();
    }
}
