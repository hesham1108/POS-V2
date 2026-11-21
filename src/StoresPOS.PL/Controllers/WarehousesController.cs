using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.WarehousesView)]
public class WarehousesController : Controller
{
    private readonly IWarehouseService _warehouses;
    private readonly IProductService _products;
    private readonly IStringLocalizer<SharedResource> _l;

    public WarehousesController(
        IWarehouseService warehouses,
        IProductService products,
        IStringLocalizer<SharedResource> localizer)
    {
        _warehouses = warehouses;
        _products = products;
        _l = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeInactive, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["Warehouses"].Value;
        ViewData["Subtitle"] = _l["Stock locations and transfers between them."].Value;
        return View(new WarehouseIndexViewModel
        {
            IncludeInactive = includeInactive,
            Warehouses = await _warehouses.ListAsync(includeInactive, cancellationToken),
            CanManage = User.HasPermission(AppPermissions.WarehousesManage),
            CanTransfer = User.HasPermission(AppPermissions.WarehousesTransfer)
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.WarehousesManage)]
    public IActionResult Create()
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["Add warehouse"].Value;
        return View("Edit", new WarehouseEditViewModel { IsActive = true });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.WarehousesManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var row = await _warehouses.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["Edit warehouse"].Value;
        return View(new WarehouseEditViewModel
        {
            Id = row.Id,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Location = row.Location,
            IsActive = row.IsActive,
            IsDefault = row.IsDefault
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.WarehousesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WarehouseEditViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = model.Id.HasValue ? _l["Edit warehouse"].Value : _l["Add warehouse"].Value;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var form = new WarehouseFormDto
            {
                Id = model.Id,
                NameEn = model.NameEn,
                NameAr = model.NameAr,
                Location = model.Location,
                IsActive = model.IsActive,
                IsDefault = model.IsDefault
            };
            if (model.Id.HasValue)
            {
                await _warehouses.UpdateAsync(form, cancellationToken);
                TempData["Success"] = _l["Warehouse updated."].Value;
            }
            else
            {
                await _warehouses.CreateAsync(form, cancellationToken);
                TempData["Success"] = _l["Warehouse created."].Value;
            }

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            return View(model);
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.WarehousesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _warehouses.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Warehouse deactivated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index), new { includeInactive = true });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.WarehousesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _warehouses.ActivateAsync(id, cancellationToken);
            TempData["Success"] = _l["Warehouse activated."].Value;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index), new { includeInactive = true });
    }

    [HttpGet]
    public async Task<IActionResult> Stock(int id, string? search, CancellationToken cancellationToken)
    {
        var row = await _warehouses.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = isAr ? row.NameAr : row.NameEn;
        ViewData["Subtitle"] = _l["Stock in this warehouse"].Value;
        return View(new WarehouseStockViewModel
        {
            WarehouseId = row.Id,
            WarehouseName = isAr ? row.NameAr : row.NameEn,
            Search = search,
            Rows = await _warehouses.GetStockAsync(id, search, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Transfers(int? warehouseId, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["Stock transfers"].Value;
        ViewData["Subtitle"] = _l["Transfer list between warehouses."].Value;
        return View(new WarehouseTransferListViewModel
        {
            WarehouseId = warehouseId,
            Warehouses = await _warehouses.ListAsync(includeInactive: true, cancellationToken),
            Transfers = await _warehouses.ListTransfersAsync(warehouseId, cancellationToken),
            CanTransfer = User.HasPermission(AppPermissions.WarehousesTransfer)
        });
    }

    [HttpGet]
    public async Task<IActionResult> TransferDetails(int id, CancellationToken cancellationToken)
    {
        var transfer = await _warehouses.GetTransferAsync(id, cancellationToken);
        if (transfer is null)
        {
            return NotFound();
        }

        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = transfer.TransferNumber;
        ViewData["Subtitle"] = _l["Stock transfer"].Value;
        return View(new WarehouseTransferDetailViewModel { Transfer = transfer });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.WarehousesTransfer)]
    public async Task<IActionResult> Transfer(CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["New transfer"].Value;
        return View(await BuildTransferFormAsync(new WarehouseTransferViewModel(), cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.WarehousesTransfer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(WarehouseTransferViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Section"] = _l["Warehouses"].Value;
        ViewData["Title"] = _l["New transfer"].Value;
        model.Items = model.Items
            .Where(x => x.ProductId > 0 && x.Quantity > 0)
            .ToList();
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, _l["Add at least one product line to transfer."].Value);
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildTransferFormAsync(model, cancellationToken));
        }

        try
        {
            var created = await _warehouses.CreateTransferAsync(
                new StockTransferCreateDto
                {
                    FromWarehouseId = model.FromWarehouseId,
                    ToWarehouseId = model.ToWarehouseId,
                    Notes = model.Notes,
                    Items = model.Items.Select(x => new StockTransferLineDto
                    {
                        ProductId = x.ProductId,
                        Quantity = x.Quantity
                    }).ToList()
                },
                this.GetRequiredUserId(),
                cancellationToken);
            TempData["Success"] = _l["Transfer {0} saved."].Value.Replace("{0}", created.TransferNumber);
            return RedirectToAction(nameof(TransferDetails), new { id = created.Id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            return View(await BuildTransferFormAsync(model, cancellationToken));
        }
    }

    private async Task<WarehouseTransferViewModel> BuildTransferFormAsync(
        WarehouseTransferViewModel model,
        CancellationToken cancellationToken)
    {
        model.Warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
        var products = await _products.GetListAsync(new ProductListFilterDto { IsActive = true }, cancellationToken);
        model.ProductOptions = products
            .Select(x => new ProductOptionViewModel
            {
                Id = x.Id,
                DisplayName = $"{Ui.Name(x.NameEn, x.NameAr)} ({x.StockQuantity:0.###} {x.Unit})",
                Unit = x.Unit,
                PurchasePrice = x.PurchasePrice
            })
            .ToList();
        if (model.Items.Count == 0)
        {
            model.Items = [new()];
        }

        return model;
    }
}
