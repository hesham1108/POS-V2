using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class WarehouseService : IWarehouseService
{
    public const string MsgNotFound = "Warehouse not found.";
    public const string MsgNameRequired = "English and Arabic names are required.";
    public const string MsgCannotDelete = "Warehouses with stock or movements cannot be deleted. Deactivate instead.";
    public const string MsgInactive = "That warehouse is not active.";
    public const string MsgSameWarehouse = "Choose two different warehouses for a transfer.";
    public const string MsgNoLines = "Add at least one product line to transfer.";
    public const string MsgInvalidQty = "Transfer quantity must be greater than zero.";

    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly StoreSettings _store;
    private readonly TimeProvider _time;

    public WarehouseService(
        ApplicationDbContext db,
        IInventoryService inventory,
        IOptions<StoreSettings> store,
        TimeProvider time)
    {
        _db = db;
        _inventory = inventory;
        _store = store.Value;
        _time = time;
    }

    public Task EnsureSeededAsync(CancellationToken cancellationToken = default) =>
        _inventory.EnsureWarehouseInfrastructureAsync(cancellationToken);

    public async Task<int> GetSellingWarehouseIdAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        if (_store.SellingWarehouseId > 0)
        {
            var configured = await _db.Warehouses.AsNoTracking()
                .Where(x => x.Id == _store.SellingWarehouseId && x.IsActive)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (configured.HasValue)
            {
                return configured.Value;
            }
        }

        return await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsDefault && x.IsActive)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.Warehouses.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseListItemDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.NameEn)
            .Select(x => new
            {
                x.Id,
                x.NameEn,
                x.NameAr,
                x.Location,
                x.IsActive,
                x.IsDefault,
                TotalQuantity = x.Stocks.Sum(s => (decimal?)s.Quantity) ?? 0m,
                ProductCount = x.Stocks.Count(s => s.Quantity != 0),
                MovementCount = x.Movements.Count
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WarehouseListItemDto
        {
            Id = x.Id,
            NameEn = x.NameEn,
            NameAr = x.NameAr,
            Location = x.Location,
            IsActive = x.IsActive,
            IsDefault = x.IsDefault,
            TotalQuantity = Math.Round(x.TotalQuantity, 3, MidpointRounding.AwayFromZero),
            ProductCount = x.ProductCount,
            MovementCount = x.MovementCount
        }).ToList();
    }

    public async Task<WarehouseDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var list = await ListAsync(includeInactive: true, cancellationToken);
        var row = list.FirstOrDefault(x => x.Id == id);
        if (row is null)
        {
            return null;
        }

        var meta = await _db.Warehouses.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.CreatedAtUtc, x.UpdatedAtUtc })
            .FirstAsync(cancellationToken);

        return new WarehouseDetailDto
        {
            Id = row.Id,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Location = row.Location,
            IsActive = row.IsActive,
            IsDefault = row.IsDefault,
            TotalQuantity = row.TotalQuantity,
            ProductCount = row.ProductCount,
            MovementCount = row.MovementCount,
            CreatedAtUtc = meta.CreatedAtUtc,
            UpdatedAtUtc = meta.UpdatedAtUtc
        };
    }

    public async Task<WarehouseDetailDto> CreateAsync(WarehouseFormDto form, CancellationToken cancellationToken = default)
    {
        ValidateForm(form);
        await EnsureSeededAsync(cancellationToken);

        if (form.IsDefault)
        {
            await ClearDefaultsAsync(cancellationToken);
        }

        var entity = new Warehouse
        {
            NameEn = form.NameEn.Trim(),
            NameAr = form.NameAr.Trim(),
            Location = string.IsNullOrWhiteSpace(form.Location) ? null : form.Location.Trim(),
            IsActive = form.IsActive,
            IsDefault = form.IsDefault,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };
        _db.Warehouses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<WarehouseDetailDto> UpdateAsync(WarehouseFormDto form, CancellationToken cancellationToken = default)
    {
        if (!form.Id.HasValue)
        {
            throw new BusinessException(MsgNotFound);
        }

        ValidateForm(form);
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == form.Id.Value, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        if (form.IsDefault && !entity.IsDefault)
        {
            await ClearDefaultsAsync(cancellationToken);
        }

        if (!form.IsActive && entity.IsDefault)
        {
            throw new BusinessException("Set another warehouse as default before deactivating this one.");
        }

        entity.NameEn = form.NameEn.Trim();
        entity.NameAr = form.NameAr.Trim();
        entity.Location = string.IsNullOrWhiteSpace(form.Location) ? null : form.Location.Trim();
        entity.IsActive = form.IsActive;
        entity.IsDefault = form.IsDefault;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        if (entity.IsDefault)
        {
            throw new BusinessException("Set another warehouse as default before deactivating this one.");
        }

        entity.IsActive = false;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        entity.IsActive = true;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseStockRowDto>> GetStockAsync(
        int warehouseId,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var exists = await _db.Warehouses.AnyAsync(x => x.Id == warehouseId, cancellationToken);
        if (!exists)
        {
            throw new BusinessException(MsgNotFound);
        }

        var query =
            from p in _db.Products.AsNoTracking().Where(x => x.IsActive)
            join ws in _db.WarehouseStocks.AsNoTracking().Where(x => x.WarehouseId == warehouseId)
                on p.Id equals ws.ProductId into stocks
            from ws in stocks.DefaultIfEmpty()
            select new { Product = p, Qty = ws != null ? ws.Quantity : 0m };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Product.NameAr.Contains(term)
                || x.Product.NameEn.Contains(term)
                || (x.Product.Barcode != null && x.Product.Barcode.Contains(term)));
        }

        return await query
            .OrderBy(x => x.Product.NameEn)
            .Select(x => new WarehouseStockRowDto
            {
                ProductId = x.Product.Id,
                NameEn = x.Product.NameEn,
                NameAr = x.Product.NameAr,
                Barcode = x.Product.Barcode,
                Unit = x.Product.Unit,
                Quantity = x.Qty,
                LowStockThreshold = x.Product.LowStockThreshold
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StockTransferDetailDto> CreateTransferAsync(
        StockTransferCreateDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (dto.FromWarehouseId == dto.ToWarehouseId)
        {
            throw new BusinessException(MsgSameWarehouse);
        }

        if (dto.Items is null || dto.Items.Count == 0)
        {
            throw new BusinessException(MsgNoLines);
        }

        var userOk = await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!userOk)
        {
            throw new BusinessException("User not found or inactive.");
        }

        await EnsureSeededAsync(cancellationToken);

        var lines = dto.Items
            .GroupBy(x => x.ProductId)
            .Select(g => new TransferStockLineDto
            {
                ProductId = g.Key,
                Quantity = Math.Round(g.Sum(x => x.Quantity), 3, MidpointRounding.AwayFromZero)
            })
            .ToList();

        if (lines.Any(x => x.Quantity <= 0))
        {
            throw new BusinessException(MsgInvalidQty);
        }

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var transfer = new StockTransfer
            {
                TransferNumber = await NextTransferNumberAsync(cancellationToken),
                FromWarehouseId = dto.FromWarehouseId,
                ToWarehouseId = dto.ToWarehouseId,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedByUserId = userId,
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
                Items = lines.Select(x => new StockTransferItem
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity
                }).ToList()
            };
            _db.StockTransfers.Add(transfer);
            await _db.SaveChangesAsync(cancellationToken);

            await _inventory.ApplyTransferAsync(
                transfer.Id,
                transfer.FromWarehouseId,
                transfer.ToWarehouseId,
                lines,
                userId,
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return (await GetTransferAsync(transfer.Id, cancellationToken))!;
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

    public async Task<IReadOnlyList<StockTransferListItemDto>> ListTransfersAsync(
        int? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var query = _db.StockTransfers.AsNoTracking().AsQueryable();
        if (warehouseId.HasValue)
        {
            query = query.Where(x =>
                x.FromWarehouseId == warehouseId.Value || x.ToWarehouseId == warehouseId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .Select(x => new StockTransferListItemDto
            {
                Id = x.Id,
                TransferNumber = x.TransferNumber,
                FromWarehouseId = x.FromWarehouseId,
                FromWarehouseNameEn = x.FromWarehouse.NameEn,
                FromWarehouseNameAr = x.FromWarehouse.NameAr,
                ToWarehouseId = x.ToWarehouseId,
                ToWarehouseNameEn = x.ToWarehouse.NameEn,
                ToWarehouseNameAr = x.ToWarehouse.NameAr,
                LineCount = x.Items.Count,
                TotalQuantity = x.Items.Sum(i => i.Quantity),
                CreatedByUsername = x.CreatedByUser.Username,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StockTransferDetailDto?> GetTransferAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var header = await _db.StockTransfers.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new StockTransferDetailDto
            {
                Id = x.Id,
                TransferNumber = x.TransferNumber,
                FromWarehouseId = x.FromWarehouseId,
                FromWarehouseNameEn = x.FromWarehouse.NameEn,
                FromWarehouseNameAr = x.FromWarehouse.NameAr,
                ToWarehouseId = x.ToWarehouseId,
                ToWarehouseNameEn = x.ToWarehouse.NameEn,
                ToWarehouseNameAr = x.ToWarehouse.NameAr,
                LineCount = x.Items.Count,
                TotalQuantity = x.Items.Sum(i => i.Quantity),
                CreatedByUsername = x.CreatedByUser.Username,
                CreatedAtUtc = x.CreatedAtUtc,
                Notes = x.Notes,
                Items = x.Items.Select(i => new StockTransferItemDto
                {
                    ProductId = i.ProductId,
                    ProductNameEn = i.Product.NameEn,
                    ProductNameAr = i.Product.NameAr,
                    Unit = i.Product.Unit,
                    Quantity = i.Quantity
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return header;
    }

    private async Task<string> NextTransferNumberAsync(CancellationToken cancellationToken)
    {
        var last = await _db.StockTransfers.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => x.TransferNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last)
            && last.StartsWith("T-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(last.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
        {
            next = seq + 1;
        }

        return "T-" + next.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static void ValidateForm(WarehouseFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.NameEn) || string.IsNullOrWhiteSpace(form.NameAr))
        {
            throw new BusinessException(MsgNameRequired);
        }
    }

    private async Task ClearDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = await _db.Warehouses.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var row in defaults)
        {
            row.IsDefault = false;
            row.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
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
