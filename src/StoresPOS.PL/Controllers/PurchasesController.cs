using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.PurchasesManage)]
public class PurchasesController : Controller
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;

    public PurchasesController(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IProductService productService)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? supplierId,
        PurchaseInvoiceStatus? status,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _purchaseService.GetListAsync(
            new PurchaseInvoiceListFilterDto
            {
                Search = search,
                SupplierId = supplierId,
                Status = status
            },
            cancellationToken);

        return View(new PurchaseIndexViewModel
        {
            Search = search,
            SupplierId = supplierId,
            Status = status,
            SupplierOptions = await LoadSupplierOptionsAsync(cancellationToken),
            Invoices = invoices.Select(x => new PurchaseListItemViewModel
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                SupplierName = x.SupplierNameEn,
                InvoiceDate = x.InvoiceDate,
                Status = x.Status,
                SubTotal = x.SubTotal,
                LineCount = x.LineCount
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var invoice = await _purchaseService.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        return View(MapDetails(invoice));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormAsync(null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        NormalizeItems(model);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var userId = this.GetRequiredUserId();
            var dto = ToUpsert(model);
            var result = model.ConfirmOnSave
                ? await _purchaseService.CreateAndConfirmAsync(dto, userId, cancellationToken)
                : await _purchaseService.CreateDraftAsync(dto, userId, cancellationToken);

            TempData["Success"] = model.ConfirmOnSave
                ? "Purchase confirmed. Stock and prices updated."
                : "Draft purchase saved.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var invoice = await _purchaseService.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
        {
            TempData["Error"] = "Confirmed purchases cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(await BuildFormAsync(invoice, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        NormalizeItems(model);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            await _purchaseService.UpdateDraftAsync(id, ToUpsert(model), cancellationToken);

            if (model.ConfirmOnSave)
            {
                var confirmed = await _purchaseService.ConfirmAsync(id, this.GetRequiredUserId(), cancellationToken);
                TempData["Success"] = "Purchase confirmed. Stock and prices updated.";
                return RedirectToAction(nameof(Details), new { id = confirmed.Id });
            }

            TempData["Success"] = "Draft purchase updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _purchaseService.ConfirmAsync(id, this.GetRequiredUserId(), cancellationToken);
            TempData["Success"] = "Purchase confirmed. Stock and prices updated.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<PurchaseFormViewModel> BuildFormAsync(
        PurchaseInvoiceDetailDto? invoice,
        CancellationToken cancellationToken)
    {
        var model = new PurchaseFormViewModel
        {
            Id = invoice?.Id,
            InvoiceNumber = invoice?.InvoiceNumber,
            SupplierId = invoice?.SupplierId ?? 0,
            InvoiceDate = invoice?.InvoiceDate.Date ?? DateTime.Today,
            Notes = invoice?.Notes,
            Items = invoice?.Items.Select(x => new PurchaseLineFormViewModel
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            }).ToList() ?? [new()]
        };

        await PopulateOptionsAsync(model, cancellationToken);
        return model;
    }

    private async Task PopulateOptionsAsync(PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        model.SupplierOptions = await LoadSupplierOptionsAsync(cancellationToken);
        model.ProductOptions = await LoadProductOptionsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierOptionViewModel>> LoadSupplierOptionsAsync(
        CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetListAsync(
            new SupplierListFilterDto { IsActive = true },
            cancellationToken);

        return suppliers
            .Select(x => new SupplierOptionViewModel
            {
                Id = x.Id,
                DisplayName = $"{x.NameEn} / {x.NameAr}"
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ProductOptionViewModel>> LoadProductOptionsAsync(
        CancellationToken cancellationToken)
    {
        var products = await _productService.GetListAsync(
            new ProductListFilterDto { IsActive = true },
            cancellationToken);

        return products
            .Select(x => new ProductOptionViewModel
            {
                Id = x.Id,
                DisplayName = $"{x.NameEn} ({x.Unit})",
                PurchasePrice = x.PurchasePrice,
                Unit = x.Unit
            })
            .ToList();
    }

    private static void NormalizeItems(PurchaseFormViewModel model)
    {
        model.Items = model.Items
            .Where(x => x.ProductId > 0)
            .ToList();

        if (model.Items.Count == 0)
        {
            model.Items = [new()];
        }
    }

    private static PurchaseInvoiceUpsertDto ToUpsert(PurchaseFormViewModel model) => new()
    {
        InvoiceNumber = model.InvoiceNumber,
        SupplierId = model.SupplierId,
        InvoiceDate = model.InvoiceDate,
        Notes = model.Notes,
        Items = model.Items
            .Where(x => x.ProductId > 0)
            .Select(x => new PurchaseInvoiceItemInputDto
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            })
            .ToList()
    };

    private static PurchaseDetailsViewModel MapDetails(PurchaseInvoiceDetailDto invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        SupplierName = $"{invoice.SupplierNameEn} / {invoice.SupplierNameAr}",
        InvoiceDate = invoice.InvoiceDate,
        Status = invoice.Status,
        Notes = invoice.Notes,
        SubTotal = invoice.SubTotal,
        CreatedByUsername = invoice.CreatedByUsername,
        CreatedAtUtc = invoice.CreatedAtUtc,
        ConfirmedByUsername = invoice.ConfirmedByUsername,
        ConfirmedAtUtc = invoice.ConfirmedAtUtc,
        Items = invoice.Items.Select(x => new PurchaseLineDetailsViewModel
        {
            ProductNameEn = x.ProductNameEn,
            ProductNameAr = x.ProductNameAr,
            Unit = x.Unit,
            Quantity = x.Quantity,
            UnitCost = x.UnitCost,
            LineTotal = x.LineTotal
        }).ToList()
    };
}
