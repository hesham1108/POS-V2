using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.CatalogManage)]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public ProductsController(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        bool lowStockOnly = false,
        bool includeInactive = false,
        [FromQuery] ListQuery? list = null,
        CancellationToken cancellationToken = default)
    {
        list ??= new ListQuery();
        search = list.Term ?? search;
        var page = await _productService.GetPageAsync(
            new ProductListFilterDto
            {
                Search = search,
                CategoryId = categoryId,
                LowStockOnly = lowStockOnly,
                IsActive = includeInactive ? null : true
            },
            list.ForRequest(Request),
            cancellationToken);
        var products = page.Items;

        if (Request.WantsCsv())
        {
            var l = this.Localizer();
            return CsvExport.File("products", products,
                (l["English name"].Value, x => x.NameEn),
                (l["Arabic name"].Value, x => x.NameAr),
                (l["Barcode"].Value, x => x.Barcode),
                (l["Unit"].Value, x => x.Unit),
                (l["Category"].Value, x => x.CategoryNameEn),
                (l["Purchase price"].Value, x => x.PurchasePrice),
                (l["Retail price"].Value, x => x.RetailPrice),
                (l["Wholesale price"].Value, x => x.WholesalePrice),
                (l["Stock"].Value, x => x.StockQuantity),
                (l["Active"].Value, x => x.IsActive));
        }

        var model = new ProductIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            LowStockOnly = lowStockOnly,
            IncludeInactive = includeInactive,
            CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken),
            Products = products.Select(MapListItem).ToList(),
            Page = page
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return View(MapListItem(product));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormAsync(null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }

        try
        {
            await _productService.CreateAsync(ToUpsert(model), cancellationToken);
            TempData["Success"] = "Product created.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return View(await BuildFormAsync(product, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }

        try
        {
            await _productService.UpdateAsync(id, ToUpsert(model), cancellationToken);
            TempData["Success"] = "Product updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _productService.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = "Product deactivated.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductFormViewModel> BuildFormAsync(ProductDto? product, CancellationToken cancellationToken)
    {
        return new ProductFormViewModel
        {
            Id = product?.Id,
            NameAr = product?.NameAr ?? string.Empty,
            NameEn = product?.NameEn ?? string.Empty,
            Barcode = product?.Barcode,
            Unit = product?.Unit ?? "pcs",
            CategoryId = product?.CategoryId,
            PurchasePrice = product?.PurchasePrice ?? 0,
            RetailMarkupPercent = product?.RetailMarkupPercent ?? 0,
            RetailPrice = product?.RetailPrice ?? 0,
            WholesaleMarkupPercent = product?.WholesaleMarkupPercent ?? 0,
            WholesalePrice = product?.WholesalePrice ?? 0,
            LowStockThreshold = product?.LowStockThreshold ?? 0,
            StockQuantity = product?.StockQuantity ?? 0,
            IsActive = product?.IsActive ?? true,
            CategoryOptions = await LoadCategoryOptionsAsync(cancellationToken)
        };
    }

    private async Task<IReadOnlyList<CategoryOptionViewModel>> LoadCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive: false, cancellationToken);
        return categories
            .Select(x => new CategoryOptionViewModel
            {
                Id = x.Id,
                DisplayName = Ui.Name(x.NameEn, x.NameAr)
            })
            .ToList();
    }

    private static ProductUpsertDto ToUpsert(ProductFormViewModel model) => new()
    {
        NameAr = model.NameAr,
        NameEn = model.NameEn,
        Barcode = model.Barcode,
        Unit = model.Unit,
        CategoryId = model.CategoryId,
        PurchasePrice = model.PurchasePrice,
        RetailPrice = model.RetailPrice,
        WholesalePrice = model.WholesalePrice,
        LowStockThreshold = model.LowStockThreshold,
        IsActive = model.IsActive
    };

    private static ProductListItemViewModel MapListItem(ProductDto x) => new()
    {
        Id = x.Id,
        NameAr = x.NameAr,
        NameEn = x.NameEn,
        Barcode = x.Barcode,
        Unit = x.Unit,
        CategoryName = Ui.NameOrNull(x.CategoryNameEn, x.CategoryNameAr),
        PurchasePrice = x.PurchasePrice,
        RetailPrice = x.RetailPrice,
        WholesalePrice = x.WholesalePrice,
        StockQuantity = x.StockQuantity,
        LowStockThreshold = x.LowStockThreshold,
        IsLowStock = x.IsLowStock,
        IsActive = x.IsActive
    };
}
