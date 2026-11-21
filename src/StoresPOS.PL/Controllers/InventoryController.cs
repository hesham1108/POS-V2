using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.CatalogManage)]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;
    private readonly IWarehouseService _warehouses;

    public InventoryController(
        IInventoryService inventoryService,
        ICategoryService categoryService,
        IProductService productService,
        IWarehouseService warehouses)
    {
        _inventoryService = inventoryService;
        _categoryService = categoryService;
        _productService = productService;
        _warehouses = warehouses;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        int? warehouseId,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        var balances = await _inventoryService.GetBalancesAsync(
            new StockBalanceFilterDto
            {
                Search = search,
                CategoryId = categoryId,
                WarehouseId = warehouseId,
                LowStockOnly = lowStockOnly
            },
            cancellationToken);

        var lowStock = await _inventoryService.GetLowStockAsync(cancellationToken);
        var categories = await _categoryService.GetAllAsync(includeInactive: false, cancellationToken);
        var warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken);

        return View(new InventoryIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            WarehouseId = warehouseId,
            LowStockOnly = lowStockOnly,
            LowStockCount = lowStock.Count,
            CategoryOptions = categories
                .Select(x => new CategoryOptionViewModel
                {
                    Id = x.Id,
                    DisplayName = Ui.Name(x.NameEn, x.NameAr)
                })
                .ToList(),
            WarehouseOptions = warehouses,
            Balances = balances.Select(x => new StockBalanceRowViewModel
            {
                ProductId = x.ProductId,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryName = Ui.NameOrNull(x.CategoryNameEn, x.CategoryNameAr),
                StockQuantity = x.StockQuantity,
                LowStockThreshold = x.LowStockThreshold,
                IsLowStock = x.IsLowStock,
                WarehouseName = Ui.NameOrNull(x.WarehouseNameEn, x.WarehouseNameAr)
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Movements(
        int? productId,
        int? warehouseId,
        StockMovementType? movementType,
        [FromQuery] ListQuery? list = null,
        CancellationToken cancellationToken = default)
    {
        list ??= new ListQuery();
        var page = await _inventoryService.GetMovementPageAsync(
            new StockMovementFilterDto
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                MovementType = movementType
            },
            list.ForRequest(Request),
            cancellationToken);
        var movements = page.Items;

        if (Request.WantsCsv())
        {
            var l = this.Localizer();
            return CsvExport.File("stock-movements", movements,
                (l["Date"].Value, x => x.CreatedAtUtc.ToLocalTime()),
                (l["Product"].Value, x => x.ProductNameEn),
                (l["Warehouse"].Value, x => x.WarehouseNameEn),
                (l["Type"].Value, x => x.MovementType.ToString()),
                (l["Change"].Value, x => x.QuantityChange),
                (l["Before"].Value, x => x.QuantityBefore),
                (l["After"].Value, x => x.QuantityAfter),
                (l["Reference"].Value, x => x.ReferenceType is null ? null : $"{x.ReferenceType} #{x.ReferenceId}"),
                (l["Reason"].Value, x => x.Reason),
                (l["User"].Value, x => x.CreatedByUsername));
        }

        return View(new StockMovementsIndexViewModel
        {
            Page = page,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            ProductOptions = await LoadProductOptionsAsync(cancellationToken),
            WarehouseOptions = await _warehouses.ListAsync(includeInactive: true, cancellationToken),
            Movements = movements.Select(x => new StockMovementRowViewModel
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ProductName = Ui.Name(x.ProductNameEn, x.ProductNameAr),
                Unit = x.Unit,
                WarehouseName = Ui.NameOrNull(x.WarehouseNameEn, x.WarehouseNameAr),
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
    public async Task<IActionResult> Adjust(int? productId, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouses = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
        return View(new StockAdjustmentFormViewModel
        {
            ProductId = productId ?? 0,
            WarehouseId = warehouseId
                ?? warehouses.FirstOrDefault(x => x.IsDefault)?.Id
                ?? warehouses.FirstOrDefault()?.Id
                ?? 0,
            ProductOptions = await LoadProductOptionsAsync(cancellationToken),
            WarehouseOptions = warehouses
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
            model.WarehouseOptions = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
            return View(model);
        }

        try
        {
            var dto = new StockAdjustmentDto
            {
                ProductId = model.ProductId,
                WarehouseId = model.WarehouseId,
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
            return RedirectToAction(nameof(Movements), new { productId = movement.ProductId, warehouseId = movement.WarehouseId });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            model.ProductOptions = await LoadProductOptionsAsync(cancellationToken);
            model.WarehouseOptions = await _warehouses.ListAsync(includeInactive: false, cancellationToken);
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
                DisplayName = $"{Ui.Name(x.NameEn, x.NameAr)} ({x.StockQuantity:0.###} {x.Unit})",
                PurchasePrice = x.PurchasePrice,
                Unit = x.Unit
            })
            .ToList();
    }
}
