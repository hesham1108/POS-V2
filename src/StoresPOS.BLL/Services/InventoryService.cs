using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class InventoryService : IInventoryService
{
    public const string PurchaseReferenceType = "PurchaseInvoice";
    public const string SaleReferenceType = "SalesInvoice";

    private readonly ApplicationDbContext _db;

    public InventoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        StockBalanceFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new StockBalanceFilterDto();
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!filter.IncludeInactive)
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

        return await query
            .OrderBy(x => x.NameEn)
            .Select(x => new StockBalanceDto
            {
                ProductId = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryNameEn = x.Category != null ? x.Category.NameEn : null,
                StockQuantity = x.StockQuantity,
                LowStockThreshold = x.LowStockThreshold,
                IsLowStock = x.StockQuantity <= x.LowStockThreshold,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<StockBalanceDto>> GetLowStockAsync(CancellationToken cancellationToken = default) =>
        GetBalancesAsync(new StockBalanceFilterDto { LowStockOnly = true }, cancellationToken);

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        StockMovementFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new StockMovementFilterDto();
        var take = filter.Take <= 0 ? 100 : Math.Min(filter.Take, 500);

        var query = _db.StockMovements.AsNoTracking().AsQueryable();

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.ProductId == filter.ProductId.Value);
        }

        if (filter.MovementType.HasValue)
        {
            query = query.Where(x => x.MovementType == filter.MovementType.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= filter.ToUtc.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new StockMovementDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductNameEn = x.Product.NameEn,
                ProductNameAr = x.Product.NameAr,
                Unit = x.Product.Unit,
                MovementType = x.MovementType,
                QuantityChange = x.QuantityChange,
                QuantityBefore = x.QuantityBefore,
                QuantityAfter = x.QuantityAfter,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                Reason = x.Reason,
                CreatedByUsername = x.CreatedByUser.Username,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StockMovementDto> AdjustStockAsync(
        StockAdjustmentDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessException("Adjustment reason is required.");
        }

        if (!dto.QuantityChange.HasValue && !dto.SetQuantityTo.HasValue)
        {
            throw new BusinessException("Provide a quantity change or a counted quantity.");
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId, cancellationToken)
                ?? throw new BusinessException("Product not found.");

            if (!product.IsActive)
            {
                throw new BusinessException("Cannot adjust stock for an inactive product.");
            }

            decimal change;
            if (dto.SetQuantityTo.HasValue)
            {
                if (dto.SetQuantityTo.Value < 0)
                {
                    throw new BusinessException("Counted quantity cannot be negative.");
                }

                change = Math.Round(dto.SetQuantityTo.Value - product.StockQuantity, 3, MidpointRounding.AwayFromZero);
            }
            else
            {
                change = Math.Round(dto.QuantityChange!.Value, 3, MidpointRounding.AwayFromZero);
            }

            if (change == 0)
            {
                throw new BusinessException("Adjustment results in no stock change.");
            }

            var movement = await ApplyQuantityChangeAsync(
                product,
                change,
                StockMovementType.Adjustment,
                referenceType: null,
                referenceId: null,
                reason: dto.Reason.Trim(),
                userId,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return await _db.StockMovements
                .AsNoTracking()
                .Where(x => x.Id == movement.Id)
                .Select(x => new StockMovementDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ProductNameEn = x.Product.NameEn,
                    ProductNameAr = x.Product.NameAr,
                    Unit = x.Product.Unit,
                    MovementType = x.MovementType,
                    QuantityChange = x.QuantityChange,
                    QuantityBefore = x.QuantityBefore,
                    QuantityAfter = x.QuantityAfter,
                    ReferenceType = x.ReferenceType,
                    ReferenceId = x.ReferenceId,
                    Reason = x.Reason,
                    CreatedByUsername = x.CreatedByUser.Username,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .SingleAsync(cancellationToken);
        }
        catch
        {
            if (tx is not null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task ApplyPurchaseReceiptAsync(
        int purchaseInvoiceId,
        IReadOnlyList<PurchaseStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Purchase has no stock lines.");
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new BusinessException($"Product #{line.ProductId} was not found.");
            }

            if (!product.IsActive)
            {
                throw new BusinessException($"Product '{product.NameEn}' is inactive.");
            }

            if (line.Quantity <= 0)
            {
                throw new BusinessException("Purchase quantity must be greater than zero.");
            }

            await ApplyQuantityChangeAsync(
                product,
                Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero),
                StockMovementType.Purchase,
                PurchaseReferenceType,
                purchaseInvoiceId,
                reason: null,
                userId,
                cancellationToken);

            product.PurchasePrice = PricingCalculator.RoundMoney(line.UnitCost);
            product.RetailPrice = PricingCalculator.CalculateSellPrice(
                product.PurchasePrice,
                product.RetailMarkupPercent);
            product.WholesalePrice = PricingCalculator.CalculateSellPrice(
                product.PurchasePrice,
                product.WholesaleMarkupPercent);
            product.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public async Task ApplySaleAsync(
        int salesInvoiceId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Sale has no stock lines.");
        }

        var grouped = lines
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        var productIds = grouped.Select(x => x.ProductId).ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in grouped)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new BusinessException($"Product #{line.ProductId} was not found.");
            }

            if (line.Quantity <= 0)
            {
                throw new BusinessException("Sale quantity must be greater than zero.");
            }

            await ApplyQuantityChangeAsync(
                product,
                -Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero),
                StockMovementType.Sale,
                SaleReferenceType,
                salesInvoiceId,
                reason: null,
                userId,
                cancellationToken);
        }
    }

    private async Task<StockMovement> ApplyQuantityChangeAsync(
        Product product,
        decimal quantityChange,
        StockMovementType movementType,
        string? referenceType,
        int? referenceId,
        string? reason,
        int userId,
        CancellationToken cancellationToken)
    {
        var before = product.StockQuantity;
        var after = Math.Round(before + quantityChange, 3, MidpointRounding.AwayFromZero);

        if (after < 0)
        {
            throw new BusinessException(
                $"Stock for '{product.NameEn}' cannot go below zero (would be {after}).");
        }

        product.StockQuantity = after;
        product.UpdatedAtUtc = DateTime.UtcNow;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            MovementType = movementType,
            QuantityChange = quantityChange,
            QuantityBefore = before,
            QuantityAfter = after,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Reason = reason,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.StockMovements.Add(movement);
        await Task.CompletedTask;
        return movement;
    }

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("User not found or inactive.");
        }
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionIfNeededAsync(
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }
}
