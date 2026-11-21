using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class InventoryService : IInventoryService
{
    public const string PurchaseReferenceType = "PurchaseInvoice";
    public const string SaleReferenceType = "SalesInvoice";
    public const string SaleReturnReferenceType = "SalesReturn";
    public const string TransferReferenceType = "StockTransfer";

    private readonly ApplicationDbContext _db;

    public InventoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureWarehouseInfrastructureAsync(CancellationToken cancellationToken = default)
    {
        if (!await _db.Warehouses.AnyAsync(cancellationToken))
        {
            _db.Warehouses.Add(new Warehouse
            {
                NameEn = "Main warehouse",
                NameAr = "المخزن الرئيسي",
                IsActive = true,
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        var defaultId = await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsDefault)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.Warehouses.AsNoTracking().Select(x => x.Id).FirstAsync(cancellationToken);

        // Migrate Product.StockQuantity into the default warehouse when no warehouse rows exist yet.
        var productsMissingStock = await _db.Products
            .Where(p => !_db.WarehouseStocks.Any(ws => ws.ProductId == p.Id))
            .Select(p => new { p.Id, p.StockQuantity })
            .ToListAsync(cancellationToken);

        if (productsMissingStock.Count > 0)
        {
            foreach (var p in productsMissingStock)
            {
                _db.WarehouseStocks.Add(new WarehouseStock
                {
                    WarehouseId = defaultId,
                    ProductId = p.Id,
                    Quantity = Math.Round(p.StockQuantity, 3, MidpointRounding.AwayFromZero)
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // Backfill movements created before warehouses (SQL migration also does this).
        var orphanMovements = await _db.StockMovements
            .Where(x => x.WarehouseId == 0)
            .ToListAsync(cancellationToken);
        if (orphanMovements.Count > 0)
        {
            foreach (var m in orphanMovements)
            {
                m.WarehouseId = defaultId;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        StockBalanceFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        filter ??= new StockBalanceFilterDto();

        if (filter.WarehouseId.HasValue)
        {
            var warehouseId = filter.WarehouseId.Value;
            var query =
                from p in _db.Products.AsNoTracking()
                join ws in _db.WarehouseStocks.AsNoTracking().Where(x => x.WarehouseId == warehouseId)
                    on p.Id equals ws.ProductId into stocks
                from ws in stocks.DefaultIfEmpty()
                join w in _db.Warehouses.AsNoTracking() on warehouseId equals w.Id
                select new { Product = p, Qty = ws != null ? ws.Quantity : 0m, Warehouse = w };

            if (!filter.IncludeInactive)
            {
                query = query.Where(x => x.Product.IsActive);
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(x => x.Product.CategoryId == filter.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(x =>
                    x.Product.NameAr.Contains(term)
                    || x.Product.NameEn.Contains(term)
                    || (x.Product.Barcode != null && x.Product.Barcode.Contains(term)));
            }

            if (filter.LowStockOnly)
            {
                query = query.Where(x => x.Qty <= x.Product.LowStockThreshold);
            }

            return await query
                .OrderBy(x => x.Product.NameEn)
                .Select(x => new StockBalanceDto
                {
                    ProductId = x.Product.Id,
                    NameAr = x.Product.NameAr,
                    NameEn = x.Product.NameEn,
                    Barcode = x.Product.Barcode,
                    Unit = x.Product.Unit,
                    CategoryNameEn = x.Product.Category != null ? x.Product.Category.NameEn : null,
                    CategoryNameAr = x.Product.Category != null ? x.Product.Category.NameAr : null,
                    StockQuantity = x.Qty,
                    LowStockThreshold = x.Product.LowStockThreshold,
                    IsLowStock = x.Qty <= x.Product.LowStockThreshold,
                    IsActive = x.Product.IsActive,
                    WarehouseId = x.Warehouse.Id,
                    WarehouseNameEn = x.Warehouse.NameEn,
                    WarehouseNameAr = x.Warehouse.NameAr
                })
                .ToListAsync(cancellationToken);
        }

        var productQuery = _db.Products.AsNoTracking().AsQueryable();
        if (!filter.IncludeInactive)
        {
            productQuery = productQuery.Where(x => x.IsActive);
        }

        if (filter.CategoryId.HasValue)
        {
            productQuery = productQuery.Where(x => x.CategoryId == filter.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            productQuery = productQuery.Where(x =>
                x.NameAr.Contains(term)
                || x.NameEn.Contains(term)
                || (x.Barcode != null && x.Barcode.Contains(term)));
        }

        if (filter.LowStockOnly)
        {
            productQuery = productQuery.Where(x => x.StockQuantity <= x.LowStockThreshold);
        }

        return await productQuery
            .OrderBy(x => x.NameEn)
            .Select(x => new StockBalanceDto
            {
                ProductId = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Barcode = x.Barcode,
                Unit = x.Unit,
                CategoryNameEn = x.Category != null ? x.Category.NameEn : null,
                CategoryNameAr = x.Category != null ? x.Category.NameAr : null,
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
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        filter ??= new StockMovementFilterDto();
        var take = filter.Take <= 0 ? 100 : Math.Min(filter.Take, 500);
        return await BuildMovementQuery(filter).Take(take).ToListAsync(cancellationToken);
    }

    public static readonly string[] MovementSortColumns =
        [nameof(StockMovementDto.CreatedAtUtc), nameof(StockMovementDto.ProductNameEn), nameof(StockMovementDto.WarehouseNameEn),
         nameof(StockMovementDto.MovementType), nameof(StockMovementDto.QuantityChange), nameof(StockMovementDto.QuantityAfter)];

    public async Task<PagedResult<StockMovementDto>> GetMovementPageAsync(
        StockMovementFilterDto filter,
        ListQuery list,
        CancellationToken cancellationToken = default)
    {
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        return await BuildMovementQuery(filter).ToPagedAsync(list, MovementSortColumns, allowAnyPageSize: list.PageSize > 100, cancellationToken: cancellationToken);
    }

    private IQueryable<StockMovementDto> BuildMovementQuery(StockMovementFilterDto? filter)
    {
        filter ??= new StockMovementFilterDto();

        var query = _db.StockMovements.AsNoTracking().AsQueryable();

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.ProductId == filter.ProductId.Value);
        }

        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
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

        return query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new StockMovementDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductNameEn = x.Product.NameEn,
                ProductNameAr = x.Product.NameAr,
                Unit = x.Product.Unit,
                WarehouseId = x.WarehouseId,
                WarehouseNameEn = x.Warehouse.NameEn,
                WarehouseNameAr = x.Warehouse.NameAr,
                MovementType = x.MovementType,
                QuantityChange = x.QuantityChange,
                QuantityBefore = x.QuantityBefore,
                QuantityAfter = x.QuantityAfter,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                Reason = x.Reason,
                CreatedByUsername = x.CreatedByUser.Username,
                CreatedAtUtc = x.CreatedAtUtc
            });
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
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        var warehouseId = await ResolveWarehouseIdAsync(dto.WarehouseId, cancellationToken);

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId, cancellationToken)
                ?? throw new BusinessException("Product not found.");

            if (!product.IsActive)
            {
                throw new BusinessException("Cannot adjust stock for an inactive product.");
            }

            var stock = await GetOrCreateStockAsync(warehouseId, product.Id, cancellationToken);
            decimal change;
            if (dto.SetQuantityTo.HasValue)
            {
                if (dto.SetQuantityTo.Value < 0)
                {
                    throw new BusinessException("Counted quantity cannot be negative.");
                }

                change = Math.Round(dto.SetQuantityTo.Value - stock.Quantity, 3, MidpointRounding.AwayFromZero);
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
                stock,
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

            return await MapMovementAsync(movement.Id, cancellationToken);
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
        int warehouseId,
        IReadOnlyList<PurchaseStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Purchase has no stock lines.");
        }

        await EnsureUserExistsAsync(userId, cancellationToken);
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        warehouseId = await ResolveWarehouseIdAsync(warehouseId, cancellationToken);
        await EnsureWarehouseActiveAsync(warehouseId, cancellationToken);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new BusinessException("Product #{0} was not found.", line.ProductId);
            }

            if (!product.IsActive)
            {
                throw new BusinessException("Product '{0}' is inactive.", new DisplayName(product.NameEn, product.NameAr));
            }

            if (line.Quantity <= 0)
            {
                throw new BusinessException("Purchase quantity must be greater than zero.");
            }

            // Weighted average cost over the whole stock (all warehouses), before this receipt is added.
            var qtyBefore = Math.Max(0m, product.StockQuantity);
            var costBefore = product.AverageCost > 0 ? product.AverageCost : product.PurchasePrice;
            var received = Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero);
            product.AverageCost = WeightedAverageCost(qtyBefore, costBefore, received, line.UnitCost);

            var stock = await GetOrCreateStockAsync(warehouseId, product.Id, cancellationToken);
            await ApplyQuantityChangeAsync(
                product,
                stock,
                received,
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

    /// <summary>(qty on hand × average cost + qty received × unit cost) ÷ total qty, rounded to 4 decimals.</summary>
    public static decimal WeightedAverageCost(decimal qtyOnHand, decimal averageCost, decimal qtyReceived, decimal unitCost)
    {
        var total = qtyOnHand + qtyReceived;
        if (qtyOnHand <= 0 || total <= 0)
        {
            return Math.Round(unitCost, 4, MidpointRounding.AwayFromZero);
        }

        return Math.Round((qtyOnHand * averageCost + qtyReceived * unitCost) / total, 4, MidpointRounding.AwayFromZero);
    }

    public async Task ApplySaleAsync(
        int salesInvoiceId,
        int warehouseId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Sale has no stock lines.");
        }

        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        warehouseId = await ResolveWarehouseIdAsync(warehouseId, cancellationToken);

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
                throw new BusinessException("Product #{0} was not found.", line.ProductId);
            }

            if (line.Quantity <= 0)
            {
                throw new BusinessException("Sale quantity must be greater than zero.");
            }

            var stock = await GetOrCreateStockAsync(warehouseId, product.Id, cancellationToken);
            await ApplyQuantityChangeAsync(
                product,
                stock,
                -Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero),
                StockMovementType.Sale,
                SaleReferenceType,
                salesInvoiceId,
                reason: null,
                userId,
                cancellationToken);
        }
    }

    public async Task ApplySaleReturnAsync(
        int salesReturnId,
        int warehouseId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Return has no stock lines.");
        }

        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        warehouseId = await ResolveWarehouseIdAsync(warehouseId, cancellationToken);

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
                throw new BusinessException("Product #{0} was not found.", line.ProductId);
            }

            if (line.Quantity <= 0)
            {
                throw new BusinessException("Return quantity must be greater than zero.");
            }

            var stock = await GetOrCreateStockAsync(warehouseId, product.Id, cancellationToken);
            await ApplyQuantityChangeAsync(
                product,
                stock,
                Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero),
                StockMovementType.SaleReturn,
                SaleReturnReferenceType,
                salesReturnId,
                reason: null,
                userId,
                cancellationToken);
        }
    }

    public async Task ApplyTransferAsync(
        int stockTransferId,
        int fromWarehouseId,
        int toWarehouseId,
        IReadOnlyList<TransferStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException("Transfer has no stock lines.");
        }

        if (fromWarehouseId == toWarehouseId)
        {
            throw new BusinessException("Choose two different warehouses for a transfer.");
        }

        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        await EnsureWarehouseActiveAsync(fromWarehouseId, cancellationToken);
        await EnsureWarehouseActiveAsync(toWarehouseId, cancellationToken);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new BusinessException("Product #{0} was not found.", line.ProductId);
            }

            var qty = Math.Round(line.Quantity, 3, MidpointRounding.AwayFromZero);
            if (qty <= 0)
            {
                throw new BusinessException("Transfer quantity must be greater than zero.");
            }

            var fromStock = await GetOrCreateStockAsync(fromWarehouseId, product.Id, cancellationToken);
            var toStock = await GetOrCreateStockAsync(toWarehouseId, product.Id, cancellationToken);

            // Out: warehouse qty drops; product total unchanged net after In.
            await ApplyQuantityChangeAsync(
                product,
                fromStock,
                -qty,
                StockMovementType.Transfer,
                TransferReferenceType,
                stockTransferId,
                reason: null,
                userId,
                cancellationToken,
                updateProductTotal: false);

            await ApplyQuantityChangeAsync(
                product,
                toStock,
                qty,
                StockMovementType.Transfer,
                TransferReferenceType,
                stockTransferId,
                reason: null,
                userId,
                cancellationToken,
                updateProductTotal: false);
        }
    }

    public async Task<decimal> GetWarehouseQuantityAsync(
        int warehouseId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        await EnsureWarehouseInfrastructureAsync(cancellationToken);
        warehouseId = await ResolveWarehouseIdAsync(warehouseId, cancellationToken);
        return await _db.WarehouseStocks.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.ProductId == productId)
            .Select(x => x.Quantity)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<StockMovement> ApplyQuantityChangeAsync(
        Product product,
        WarehouseStock stock,
        decimal quantityChange,
        StockMovementType movementType,
        string? referenceType,
        int? referenceId,
        string? reason,
        int userId,
        CancellationToken cancellationToken,
        bool updateProductTotal = true)
    {
        var before = stock.Quantity;
        var after = Math.Round(before + quantityChange, 3, MidpointRounding.AwayFromZero);

        if (after < 0)
        {
            throw new BusinessException(
                "Stock for '{0}' cannot go below zero in this warehouse (would be {1}).", new DisplayName(product.NameEn, product.NameAr), after);
        }

        stock.Quantity = after;

        if (updateProductTotal)
        {
            var productAfter = Math.Round(product.StockQuantity + quantityChange, 3, MidpointRounding.AwayFromZero);
            if (productAfter < 0)
            {
                throw new BusinessException(
                    "Stock for '{0}' cannot go below zero (would be {1}).", new DisplayName(product.NameEn, product.NameAr), productAfter);
            }

            product.StockQuantity = productAfter;
            product.UpdatedAtUtc = DateTime.UtcNow;
        }

        var movement = new StockMovement
        {
            ProductId = product.Id,
            WarehouseId = stock.WarehouseId,
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

    private async Task<WarehouseStock> GetOrCreateStockAsync(
        int warehouseId,
        int productId,
        CancellationToken cancellationToken)
    {
        var stock = await _db.WarehouseStocks
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId, cancellationToken);
        if (stock is not null)
        {
            return stock;
        }

        stock = new WarehouseStock
        {
            WarehouseId = warehouseId,
            ProductId = productId,
            Quantity = 0m
        };
        _db.WarehouseStocks.Add(stock);
        await _db.SaveChangesAsync(cancellationToken);
        return stock;
    }

    private async Task<int> ResolveWarehouseIdAsync(int warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId > 0)
        {
            var exists = await _db.Warehouses.AnyAsync(x => x.Id == warehouseId, cancellationToken);
            if (!exists)
            {
                throw new BusinessException("Warehouse not found.");
            }

            return warehouseId;
        }

        return await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsDefault)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.Warehouses.AsNoTracking().Select(x => x.Id).FirstAsync(cancellationToken);
    }

    private async Task EnsureWarehouseActiveAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var active = await _db.Warehouses.AsNoTracking()
            .Where(x => x.Id == warehouseId)
            .Select(x => (bool?)x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (active is null)
        {
            throw new BusinessException("Warehouse not found.");
        }

        if (!active.Value)
        {
            throw new BusinessException("That warehouse is not active.");
        }
    }

    private async Task<StockMovementDto> MapMovementAsync(long id, CancellationToken cancellationToken) =>
        await _db.StockMovements
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new StockMovementDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductNameEn = x.Product.NameEn,
                ProductNameAr = x.Product.NameAr,
                Unit = x.Product.Unit,
                WarehouseId = x.WarehouseId,
                WarehouseNameEn = x.Warehouse.NameEn,
                WarehouseNameAr = x.Warehouse.NameAr,
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
