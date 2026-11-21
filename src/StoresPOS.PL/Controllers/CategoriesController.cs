using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.ViewModels;

using StoresPOS.PL.Extensions;

namespace StoresPOS.PL.Controllers;

[Authorize(Policy = AppPermissions.CatalogManage)]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _categoryService.GetAllAsync(includeInactive: true, cancellationToken);
        var model = items.Select(x => new CategoryListItemViewModel
        {
            Id = x.Id,
            NameAr = x.NameAr,
            NameEn = x.NameEn,
            ParentName = Ui.NameOrNull(x.ParentNameEn, x.ParentNameAr),
            SortOrder = x.SortOrder,
            ProductCount = x.ProductCount,
            ChildCount = x.ChildCount,
            IsActive = x.IsActive
        }).ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormAsync(null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.ParentOptions = await LoadParentOptionsAsync(excludeId: null, cancellationToken);
            return View(model);
        }

        try
        {
            await _categoryService.CreateAsync(ToUpsert(model), cancellationToken);
            TempData["Success"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            model.ParentOptions = await LoadParentOptionsAsync(excludeId: null, cancellationToken);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        return View(await BuildFormAsync(category, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.ParentOptions = await LoadParentOptionsAsync(excludeId: id, cancellationToken);
            return View(model);
        }

        try
        {
            await _categoryService.UpdateAsync(id, ToUpsert(model), cancellationToken);
            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, this.Localizer().Localize(ex));
            model.ParentOptions = await LoadParentOptionsAsync(excludeId: id, cancellationToken);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int? parentCategoryId, CancellationToken cancellationToken)
    {
        try
        {
            await _categoryService.MoveAsync(id, parentCategoryId, cancellationToken);
            TempData["Success"] = "Category moved.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromForm] List<CategoryReorderViewModel> items, CancellationToken cancellationToken)
    {
        try
        {
            await _categoryService.ReorderAsync(
                items.Select(x => new CategoryReorderItemDto { Id = x.Id, SortOrder = x.SortOrder }).ToList(),
                cancellationToken);
            TempData["Success"] = "Categories reordered.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _categoryService.DeactivateAsync(id, cancellationToken);
            TempData["Success"] = "Category deactivated.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = this.Localizer().Localize(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CategoryFormViewModel> BuildFormAsync(CategoryDto? category, CancellationToken cancellationToken)
    {
        return new CategoryFormViewModel
        {
            Id = category?.Id,
            NameAr = category?.NameAr ?? string.Empty,
            NameEn = category?.NameEn ?? string.Empty,
            ParentCategoryId = category?.ParentCategoryId,
            SortOrder = category?.SortOrder,
            IsActive = category?.IsActive ?? true,
            ParentOptions = await LoadParentOptionsAsync(category?.Id, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<CategoryOptionViewModel>> LoadParentOptionsAsync(
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var all = await _categoryService.GetAllAsync(includeInactive: false, cancellationToken);
        return all
            .Where(x => excludeId is null || x.Id != excludeId.Value)
            .Select(x => new CategoryOptionViewModel
            {
                Id = x.Id,
                DisplayName = string.IsNullOrWhiteSpace(x.ParentNameEn)
                    ? Ui.Name(x.NameEn, x.NameAr)
                    : $"{Ui.Name(x.ParentNameEn, x.ParentNameAr)} › {Ui.Name(x.NameEn, x.NameAr)}"
            })
            .ToList();
    }

    private static CategoryUpsertDto ToUpsert(CategoryFormViewModel model) => new()
    {
        NameAr = model.NameAr,
        NameEn = model.NameEn,
        ParentCategoryId = model.ParentCategoryId,
        SortOrder = model.SortOrder,
        IsActive = model.IsActive
    };
}
