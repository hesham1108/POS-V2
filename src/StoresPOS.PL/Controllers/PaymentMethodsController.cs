using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.PaymentMethodsManage)]
public class PaymentMethodsController : Controller
{
    private readonly IPaymentMethodService _methods;
    private readonly ITreasuryService _treasuries;
    private readonly IStringLocalizer<SharedResource> _l;

    public PaymentMethodsController(
        IPaymentMethodService methods,
        ITreasuryService treasuries,
        IStringLocalizer<SharedResource> localizer)
    {
        _methods = methods;
        _treasuries = treasuries;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Settings"].Value;
        ViewData["Title"] = _l["Payment methods"].Value;
        ViewData["Subtitle"] = _l["Add, rename, reorder, activate or deactivate. Used methods cannot be deleted."].Value;
        return View(await _methods.ListAsync(includeInactive: true, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Payment methods"].Value;
        ViewData["Title"] = _l["Add payment method"].Value;
        return View("Edit", new PaymentMethodEditViewModel
        {
            Kind = PaymentMethodKind.Card,
            IsActive = true,
            TreasuryOptions = await _treasuries.ListAsync(includeInactive: false, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(byte id, CancellationToken cancellationToken)
    {
        var row = await _methods.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Payment methods"].Value;
        ViewData["Title"] = _l["Edit payment method"].Value;
        return View(new PaymentMethodEditViewModel
        {
            Id = row.Id,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Kind = row.Kind,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            DefaultTreasuryId = row.DefaultTreasuryId,
            TreasuryOptions = await _treasuries.ListAsync(includeInactive: false, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentMethodEditViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Payment methods"].Value;
        ViewData["Title"] = model.Id.HasValue ? _l["Edit payment method"].Value : _l["Add payment method"].Value;
        model.TreasuryOptions = await _treasuries.ListAsync(includeInactive: false, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var form = new PaymentMethodFormDto
            {
                Id = model.Id,
                NameEn = model.NameEn,
                NameAr = model.NameAr,
                Kind = model.Kind,
                IsActive = model.IsActive,
                SortOrder = model.SortOrder,
                DefaultTreasuryId = model.DefaultTreasuryId
            };
            if (model.Id.HasValue)
            {
                await _methods.UpdateAsync(form, cancellationToken);
            }
            else
            {
                await _methods.CreateAsync(form, cancellationToken);
            }

            TempData["Success"] = _l["Saved."].Value;
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
    public async Task<IActionResult> Deactivate(byte id, CancellationToken cancellationToken)
    {
        try
        {
            await _methods.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Deactivated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(byte id, CancellationToken cancellationToken)
    {
        try
        {
            await _methods.ActivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Activated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(List<PaymentMethodReorderItemDto> items, CancellationToken cancellationToken)
    {
        await _methods.ReorderAsync(items ?? [], cancellationToken);
        TempData["Success"] = _l["Order saved."].Value;
        return RedirectToAction(nameof(Index));
    }
}
