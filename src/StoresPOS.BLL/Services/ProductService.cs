using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;

    public ProductService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductDto>> GetListAsync(
        ProductListFilterDto? filter = null,
        CancellationToken cancellationToken = default) =>
        await BuildListQuery(filter).ToListAsync(cancellationToken);

    public static readonly string[] SortColumns =
        [nameof(ProductDto.NameEn), nameof(ProductDto.NameAr), nameof(ProductDto.Barcode), nameof(ProductDto.CategoryNameEn),
         nameof(ProductDto.PurchasePrice), nameof(ProductDto.RetailPrice), nameof(ProductDto.WholesalePrice), nameof(ProductDto.StockQuantity)];

    public Task<PagedResult<ProductDto>> GetPageAsync(
        ProductListFilterDto filter,
        ListQuery list,
        CancellationToken cancellationToken = default) =>
        BuildListQuery(filter).ToPagedAsync(list, SortColumns, allowAnyPageSize: list.PageSize > 100, cancellationToken: cancellationToken);

    private IQueryable<ProductDto> BuildListQuery(ProductListFilterDto? filter)
    {
        filter ??= new ProductListFilterDto();
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }
        else
        {
            query = query.Where(x => x.IsActive);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.NameAr.Contains(term)
                || x.NameEn.Contains(term)
                || (x.Barcode != null && x.Barcode.Contains(term)));
        }

        if (filter.LowStockOnly)
        {
            query = query.Where(x => x.StockQuantity <= x.LowStockThreshold);
        }

        return query
            .OrderBy(x => x.NameEn)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryId = x.CategoryId,
                CategoryNameAr = x.Category != null ? x.Category.NameAr : null,
                CategoryNameEn = x.Category != null ? x.Category.NameEn : null,
                PurchasePrice = x.PurchasePrice,
                RetailMarkupPercent = x.RetailMarkupPercent,
                RetailPrice = x.RetailPrice,
                WholesaleMarkupPercent = x.WholesaleMarkupPercent,
                WholesalePrice = x.WholesalePrice,
                StockQuantity = x.StockQuantity,
                LowStockThreshold = x.LowStockThreshold,
                IsLowStock = x.StockQuantity <= x.LowStockThreshold,
                IsActive = x.IsActive
            });
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryId = x.CategoryId,
                CategoryNameAr = x.Category != null ? x.Category.NameAr : null,
                CategoryNameEn = x.Category != null ? x.Category.NameEn : null,
                PurchasePrice = x.PurchasePrice,
                RetailMarkupPercent = x.RetailMarkupPercent,
                RetailPrice = x.RetailPrice,
                WholesaleMarkupPercent = x.WholesaleMarkupPercent,
                WholesalePrice = x.WholesalePrice,
                StockQuantity = x.StockQuantity,
                LowStockThreshold = x.LowStockThreshold,
                IsLowStock = x.StockQuantity <= x.LowStockThreshold,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(ProductUpsertDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);
        await EnsureCategoryExistsAsync(dto.CategoryId, cancellationToken);
        await EnsureBarcodeUniqueAsync(dto.Barcode, excludeProductId: null, cancellationToken);

        var (retailPrice, retailMarkup) = PricingCalculator.ResolvePricePair(
            dto.PurchasePrice,
            dto.RetailPrice,
            dto.RetailMarkupPercent);

        var (wholesalePrice, wholesaleMarkup) = PricingCalculator.ResolvePricePair(
            dto.PurchasePrice,
            dto.WholesalePrice,
            dto.WholesaleMarkupPercent);

        var entity = new Product
        {
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            Barcode = NormalizeBarcode(dto.Barcode),
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "pcs" : dto.Unit.Trim(),
            CategoryId = dto.CategoryId,
            PurchasePrice = PricingCalculator.RoundMoney(dto.PurchasePrice),
            AverageCost = PricingCalculator.RoundMoney(dto.PurchasePrice),
            RetailPrice = retailPrice,
            RetailMarkupPercent = retailMarkup,
            WholesalePrice = wholesalePrice,
            WholesaleMarkupPercent = wholesaleMarkup,
            StockQuantity = 0,
            LowStockThreshold = dto.LowStockThreshold < 0 ? 0 : dto.LowStockThreshold,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductUpsertDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);

        var entity = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Product not found.");

        await EnsureCategoryExistsAsync(dto.CategoryId, cancellationToken);
        await EnsureBarcodeUniqueAsync(dto.Barcode, excludeProductId: id, cancellationToken);

        var (retailPrice, retailMarkup) = PricingCalculator.ResolvePricePair(
            dto.PurchasePrice,
            dto.RetailPrice,
            dto.RetailMarkupPercent);

        var (wholesalePrice, wholesaleMarkup) = PricingCalculator.ResolvePricePair(
            dto.PurchasePrice,
            dto.WholesalePrice,
            dto.WholesaleMarkupPercent);

        entity.NameAr = dto.NameAr.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.Barcode = NormalizeBarcode(dto.Barcode);
        entity.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "pcs" : dto.Unit.Trim();
        entity.CategoryId = dto.CategoryId;
        entity.PurchasePrice = PricingCalculator.RoundMoney(dto.PurchasePrice);
        entity.RetailPrice = retailPrice;
        entity.RetailMarkupPercent = retailMarkup;
        entity.WholesalePrice = wholesalePrice;
        entity.WholesaleMarkupPercent = wholesaleMarkup;
        entity.LowStockThreshold = dto.LowStockThreshold < 0 ? 0 : dto.LowStockThreshold;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Product not found.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(ProductUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NameAr))
        {
            throw new BusinessException("Arabic name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.NameEn))
        {
            throw new BusinessException("English name is required.");
        }

        if (dto.PurchasePrice < 0)
        {
            throw new BusinessException("Purchase price cannot be negative.");
        }
    }

    private async Task EnsureCategoryExistsAsync(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        var exists = await _db.Categories.AnyAsync(x => x.Id == categoryId.Value && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("Category not found or inactive.");
        }
    }

    private async Task EnsureBarcodeUniqueAsync(
        string? barcode,
        int? excludeProductId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeBarcode(barcode);
        if (normalized is null)
        {
            return;
        }

        var query = _db.Products.Where(x => x.Barcode == normalized);
        if (excludeProductId.HasValue)
        {
            query = query.Where(x => x.Id != excludeProductId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BusinessException("Barcode is already used by another product.");
        }
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        return barcode.Trim();
    }
}
