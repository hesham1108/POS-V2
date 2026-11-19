using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.CatalogManage)]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;

    public InventoryController(
        IInventoryService inventoryService,
        ICategoryService categoryService,
        IProductService productService)
    {
        _inventoryService = inventoryService;
        _categoryService = categoryService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        var balances = await _inventoryService.GetBalancesAsync(
            new StockBalanceFilterDto
            {
                Search = search,
                CategoryId = categoryId,
                LowStockOnly = lowStockOnly
            },
            cancellationToken);

        var lowStock = await _inventoryService.GetLowStockAsync(cancellationToken);
        var categories = await _categoryService.GetAllAsync(includeInactive: false, cancellationToken);

        return View(new InventoryIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            LowStockOnly = lowStockOnly,
            LowStockCount = lowStock.Count,
            CategoryOptions = categories
                .Select(x => new CategoryOptionViewModel
                {
                    Id = x.Id,
                    DisplayName = $"{x.NameEn} / {x.NameAr}"
                })
                .ToList(),
            Balances = balances.Select(x => new StockBalanceRowViewModel
            {
                ProductId = x.ProductId,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryName = x.CategoryNameEn,
                StockQuantity = x.StockQuantity,
                LowStockThreshold = x.LowStockThreshold,
                IsLowStock = x.IsLowStock
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Movements(
        int? productId,
        StockMovementType? movementType,
        CancellationToken cancellationToken = default)
    {
        var movements = await _inventoryService.GetMovementsAsync(
            new StockMovementFilterDto
            {
                ProductId = productId,
                MovementType = movementType,
                Take = 200
            },
            cancellationToken);

        return View(new StockMovementsIndexViewModel
        {
            ProductId = productId,
            MovementType = movementType,
            ProductOptions = await LoadProductOptionsAsync(cancellationToken),
            Movements = movements.Select(x => new StockMovementRowViewModel
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ProductName = x.ProductNameEn,
                Unit = x.Unit,
                MovementType = x.MovementType,
                QuantityChange = x.QuantityChange,
                QuantityBefore = x.QuantityBefore,
                QuantityAfter = x.QuantityAfter,
                Reference = x.ReferenceType is null
                    ? null
                    : $"{x.ReferenceType} #{x.ReferenceId}",
                Reason = x.Reason,
                CreatedByUsername = x.CreatedByUsername
            }).ToList()
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPermissions.InventoryAdjust)]
    public async Task<IActionResult> Adjust(int? productId, CancellationToken cancellationToken = default)
    {
        return View(new StockAdjustmentFormViewModel
        {
            ProductId = productId ?? 0,
            ProductOptions = await LoadProductOptionsAsync(cancellationToken)
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.InventoryAdjust)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(StockAdjustmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.ProductOptions = await LoadProductOptionsAsync(cancellationToken);
            return View(model);
        }

        try
        {
            var dto = new StockAdjustmentDto
            {
                ProductId = model.ProductId,
                Reason = model.Reason
            };

            if (string.Equals(model.Mode, "set", StringComparison.OrdinalIgnoreCase))
            {
                dto.SetQuantityTo = model.SetQuantityTo;
            }
            else
            {
                dto.QuantityChange = model.QuantityChange;
            }

            var movement = await _inventoryService.AdjustStockAsync(
                dto,
                this.GetRequiredUserId(),
                cancellationToken);

            TempData["Success"] =
                $"Stock adjusted for {movement.ProductNameEn}: {movement.QuantityBefore:0.###} → {movement.QuantityAfter:0.###}";
            return RedirectToAction(nameof(Movements), new { productId = movement.ProductId });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.ProductOptions = await LoadProductOptionsAsync(cancellationToken);
            return View(model);
        }
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
                DisplayName = $"{x.NameEn} (stock {x.StockQuantity:0.###} {x.Unit})",
                PurchasePrice = x.PurchasePrice,
                Unit = x.Unit
            })
            .ToList();
    }
}
