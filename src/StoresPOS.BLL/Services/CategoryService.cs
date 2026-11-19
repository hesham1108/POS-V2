using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _db;

    public CategoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var categories = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                ParentCategoryId = x.ParentCategoryId,
                ParentNameAr = x.ParentCategory != null ? x.ParentCategory.NameAr : null,
                ParentNameEn = x.ParentCategory != null ? x.ParentCategory.NameEn : null,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                ProductCount = x.Products.Count(p => p.IsActive),
                ChildCount = x.Children.Count(c => includeInactive || c.IsActive)
            })
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<IReadOnlyList<CategoryTreeNodeDto>> GetTreeAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var flat = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .Select(x => new CategoryTreeNodeDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                ParentCategoryId = x.ParentCategoryId,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return BuildTree(flat);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                ParentCategoryId = x.ParentCategoryId,
                ParentNameAr = x.ParentCategory != null ? x.ParentCategory.NameAr : null,
                ParentNameEn = x.ParentCategory != null ? x.ParentCategory.NameEn : null,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                ProductCount = x.Products.Count(p => p.IsActive),
                ChildCount = x.Children.Count(c => c.IsActive)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(CategoryUpsertDto dto, CancellationToken cancellationToken = default)
    {
        ValidateNames(dto);
        await EnsureParentExistsAsync(dto.ParentCategoryId, cancellationToken);

        var sortOrder = dto.SortOrder
            ?? await NextSortOrderAsync(dto.ParentCategoryId, cancellationToken);

        var entity = new Category
        {
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            ParentCategoryId = dto.ParentCategoryId,
            SortOrder = sortOrder,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpsertDto dto, CancellationToken cancellationToken = default)
    {
        ValidateNames(dto);

        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Category not found.");

        if (dto.ParentCategoryId == id)
        {
            throw new BusinessException("A category cannot be its own parent.");
        }

        await EnsureParentExistsAsync(dto.ParentCategoryId, cancellationToken);

        if (dto.ParentCategoryId != entity.ParentCategoryId)
        {
            await EnsureNotDescendantAsync(id, dto.ParentCategoryId, cancellationToken);
        }

        entity.NameAr = dto.NameAr.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.ParentCategoryId = dto.ParentCategoryId;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.SortOrder.HasValue)
        {
            entity.SortOrder = dto.SortOrder.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task MoveAsync(int id, int? newParentCategoryId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Category not found.");

        if (newParentCategoryId == id)
        {
            throw new BusinessException("A category cannot be its own parent.");
        }

        await EnsureParentExistsAsync(newParentCategoryId, cancellationToken);
        await EnsureNotDescendantAsync(id, newParentCategoryId, cancellationToken);

        entity.ParentCategoryId = newParentCategoryId;
        entity.SortOrder = await NextSortOrderAsync(newParentCategoryId, cancellationToken);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        IReadOnlyList<CategoryReorderItemDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var ids = items.Select(x => x.Id).Distinct().ToList();
        var categories = await _db.Categories
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (categories.Count != ids.Count)
        {
            throw new BusinessException("One or more categories were not found for reorder.");
        }

        var map = items.ToDictionary(x => x.Id, x => x.SortOrder);
        foreach (var category in categories)
        {
            category.SortOrder = map[category.Id];
            category.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Category not found.");

        var hasActiveChildren = await _db.Categories
            .AnyAsync(x => x.ParentCategoryId == id && x.IsActive, cancellationToken);
        if (hasActiveChildren)
        {
            throw new BusinessException("Deactivate or move child categories first.");
        }

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateNames(CategoryUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NameAr))
        {
            throw new BusinessException("Arabic name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.NameEn))
        {
            throw new BusinessException("English name is required.");
        }
    }

    private async Task EnsureParentExistsAsync(int? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return;
        }

        var exists = await _db.Categories.AnyAsync(x => x.Id == parentId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("Parent category not found.");
        }
    }

    private async Task EnsureNotDescendantAsync(
        int categoryId,
        int? newParentId,
        CancellationToken cancellationToken)
    {
        if (newParentId is null)
        {
            return;
        }

        var current = newParentId;
        while (current.HasValue)
        {
            if (current.Value == categoryId)
            {
                throw new BusinessException("Cannot move a category under one of its descendants.");
            }

            current = await _db.Categories
                .Where(x => x.Id == current.Value)
                .Select(x => x.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    private async Task<int> NextSortOrderAsync(int? parentId, CancellationToken cancellationToken)
    {
        var max = await _db.Categories
            .Where(x => x.ParentCategoryId == parentId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken);

        return (max ?? -1) + 1;
    }

    private static List<CategoryTreeNodeDto> BuildTree(List<CategoryTreeNodeDto> flat)
    {
        var lookup = flat.ToDictionary(x => x.Id);
        var roots = new List<CategoryTreeNodeDto>();

        foreach (var node in flat)
        {
            if (node.ParentCategoryId is int parentId && lookup.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
