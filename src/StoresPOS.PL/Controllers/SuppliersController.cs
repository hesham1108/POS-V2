using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.PurchasesManage)]
public class SuppliersController : Controller
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _supplierService.GetListAsync(
            new SupplierListFilterDto
            {
                Search = search,
                IsActive = includeInactive ? null : true
            },
            cancellationToken);

        return View(new SupplierIndexViewModel
        {
            Search = search,
            IncludeInactive = includeInactive,
            Suppliers = list.Select(x => new SupplierListItemViewModel
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Phone = x.Phone,
                Email = x.Email,
                ConfirmedPurchaseCount = x.ConfirmedPurchaseCount,
                TotalPurchasesAmount = x.TotalPurchasesAmount,
                IsActive = x.IsActive
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new SupplierFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _supplierService.CreateAsync(ToUpsert(model), cancellationToken);
            TempData["Success"] = "Supplier created.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        return View(new SupplierFormViewModel
        {
            Id = supplier.Id,
            NameAr = supplier.NameAr,
            NameEn = supplier.NameEn,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SupplierFormViewModel model, CancellationToken cancellationToken)
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
            await _supplierService.UpdateAsync(id, ToUpsert(model), cancellationToken);
            TempData["Success"] = "Supplier updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _supplierService.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = "Supplier deactivated.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private static SupplierUpsertDto ToUpsert(SupplierFormViewModel model) => new()
    {
        NameAr = model.NameAr,
        NameEn = model.NameEn,
        Phone = model.Phone,
        Email = model.Email,
        Address = model.Address,
        Notes = model.Notes,
        IsActive = model.IsActive
    };
}
