using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class PurchaseService : IPurchaseService
{
    public const string MsgTreasuryRequired = "Choose a treasury to pay this purchase from.";

    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventoryService;
    private readonly ITreasuryService _treasuries;

    public PurchaseService(
        ApplicationDbContext db,
        IInventoryService inventoryService,
        ITreasuryService treasuries)
    {
        _db = db;
        _inventoryService = inventoryService;
        _treasuries = treasuries;
    }

    public async Task<IReadOnlyList<PurchaseInvoiceListItemDto>> GetListAsync(
        PurchaseInvoiceListFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new PurchaseInvoiceListFilterDto();
        var query = _db.PurchaseInvoices.AsNoTracking().AsQueryable();

        if (filter.SupplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == filter.SupplierId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.InvoiceNumber.Contains(term)
                || x.Supplier.NameEn.Contains(term)
                || x.Supplier.NameAr.Contains(term));
        }

        return await query
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new PurchaseInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                SupplierId = x.SupplierId,
                SupplierNameEn = x.Supplier.NameEn,
                SupplierNameAr = x.Supplier.NameAr,
                InvoiceDate = x.InvoiceDate,
                Status = x.Status,
                SubTotal = x.SubTotal,
                LineCount = x.Items.Count,
                CreatedAtUtc = x.CreatedAtUtc,
                ConfirmedAtUtc = x.ConfirmedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PurchaseInvoiceDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.PurchaseInvoices
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PurchaseInvoiceDetailDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                SupplierId = x.SupplierId,
                SupplierNameEn = x.Supplier.NameEn,
                SupplierNameAr = x.Supplier.NameAr,
                InvoiceDate = x.InvoiceDate,
                Status = x.Status,
                Notes = x.Notes,
                SubTotal = x.SubTotal,
                CreatedByUsername = x.CreatedByUser.Username,
                CreatedAtUtc = x.CreatedAtUtc,
                ConfirmedByUsername = x.ConfirmedByUser != null ? x.ConfirmedByUser.Username : null,
                ConfirmedAtUtc = x.ConfirmedAtUtc,
                PaidFromTreasuryId = x.PaidFromTreasuryId,
                PaidFromTreasuryNameEn = x.PaidFromTreasury != null ? x.PaidFromTreasury.NameEn : null,
                PaidFromTreasuryNameAr = x.PaidFromTreasury != null ? x.PaidFromTreasury.NameAr : null,
                Items = x.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new PurchaseInvoiceItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductNameAr = i.ProductNameAr,
                        ProductNameEn = i.ProductNameEn,
                        Unit = i.Unit,
                        Quantity = i.Quantity,
                        UnitCost = i.UnitCost,
                        LineTotal = i.LineTotal
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PurchaseInvoiceDetailDto> CreateDraftAsync(
        PurchaseInvoiceUpsertDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var entity = await BuildDraftEntityAsync(dto, userId, cancellationToken);
            _db.PurchaseInvoices.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return (await GetByIdAsync(entity.Id, cancellationToken))!;
        }, cancellationToken);
    }

    public async Task<PurchaseInvoiceDetailDto> UpdateDraftAsync(
        int id,
        PurchaseInvoiceUpsertDto dto,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var entity = await _db.PurchaseInvoices
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new BusinessException("Purchase invoice not found.");

            if (entity.Status != PurchaseInvoiceStatus.Draft)
            {
                throw new BusinessException("Only draft purchase invoices can be edited.");
            }

            await EnsureSupplierActiveAsync(dto.SupplierId, cancellationToken);
            var lines = await BuildLineEntitiesAsync(dto.Items, cancellationToken);
            var invoiceNumber = await ResolveInvoiceNumberAsync(dto.InvoiceNumber, excludeId: id, cancellationToken);

            entity.InvoiceNumber = invoiceNumber;
            entity.SupplierId = dto.SupplierId;
            entity.InvoiceDate = NormalizeInvoiceDate(dto.InvoiceDate);
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            entity.SubTotal = lines.Sum(x => x.LineTotal);

            _db.PurchaseInvoiceItems.RemoveRange(entity.Items);
            entity.Items = lines;

            await _db.SaveChangesAsync(cancellationToken);
            return (await GetByIdAsync(entity.Id, cancellationToken))!;
        }, cancellationToken);
    }

    public async Task<PurchaseInvoiceDetailDto> ConfirmAsync(
        int id,
        int userId,
        int paidFromTreasuryId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (paidFromTreasuryId <= 0)
            {
                throw new BusinessException(MsgTreasuryRequired);
            }

            var entity = await _db.PurchaseInvoices
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new BusinessException("Purchase invoice not found.");

            if (entity.Status != PurchaseInvoiceStatus.Draft)
            {
                throw new BusinessException("Purchase invoice is already confirmed.");
            }

            if (entity.Items.Count == 0)
            {
                throw new BusinessException("Cannot confirm a purchase with no lines.");
            }

            await ApplyPurchaseStockAsync(entity.Id, entity.Items, userId, cancellationToken);

            entity.Status = PurchaseInvoiceStatus.Confirmed;
            entity.ConfirmedAtUtc = DateTime.UtcNow;
            entity.ConfirmedByUserId = userId;
            entity.PaidFromTreasuryId = paidFromTreasuryId;

            await _treasuries.RecordPurchasePaymentAsync(
                paidFromTreasuryId, entity.SubTotal, entity.Id, userId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return (await GetByIdAsync(entity.Id, cancellationToken))!;
        }, cancellationToken);
    }

    public async Task<PurchaseInvoiceDetailDto> CreateAndConfirmAsync(
        PurchaseInvoiceUpsertDto dto,
        int userId,
        int paidFromTreasuryId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (paidFromTreasuryId <= 0)
            {
                throw new BusinessException(MsgTreasuryRequired);
            }

            var entity = await BuildDraftEntityAsync(dto, userId, cancellationToken);
            _db.PurchaseInvoices.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            await ApplyPurchaseStockAsync(entity.Id, entity.Items, userId, cancellationToken);
            entity.Status = PurchaseInvoiceStatus.Confirmed;
            entity.ConfirmedAtUtc = DateTime.UtcNow;
            entity.ConfirmedByUserId = userId;
            entity.PaidFromTreasuryId = paidFromTreasuryId;

            await _treasuries.RecordPurchasePaymentAsync(
                paidFromTreasuryId, entity.SubTotal, entity.Id, userId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return (await GetByIdAsync(entity.Id, cancellationToken))!;
        }, cancellationToken);
    }

    private async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        // InMemory (unit tests) does not support relational transactions.
        if (string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return await action();
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PurchaseInvoice> BuildDraftEntityAsync(
        PurchaseInvoiceUpsertDto dto,
        int userId,
        CancellationToken cancellationToken)
    {
        await EnsureSupplierActiveAsync(dto.SupplierId, cancellationToken);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var lines = await BuildLineEntitiesAsync(dto.Items, cancellationToken);
        var invoiceNumber = await ResolveInvoiceNumberAsync(dto.InvoiceNumber, excludeId: null, cancellationToken);

        return new PurchaseInvoice
        {
            InvoiceNumber = invoiceNumber,
            SupplierId = dto.SupplierId,
            InvoiceDate = NormalizeInvoiceDate(dto.InvoiceDate),
            Status = PurchaseInvoiceStatus.Draft,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            SubTotal = lines.Sum(x => x.LineTotal),
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Items = lines
        };
    }

    private Task ApplyPurchaseStockAsync(
        int purchaseInvoiceId,
        IEnumerable<PurchaseInvoiceItem> items,
        int userId,
        CancellationToken cancellationToken) =>
        _inventoryService.ApplyPurchaseReceiptAsync(
            purchaseInvoiceId,
            items.Select(x => new PurchaseStockLineDto
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            }).ToList(),
            userId,
            cancellationToken);

    private async Task<List<PurchaseInvoiceItem>> BuildLineEntitiesAsync(
        IReadOnlyList<PurchaseInvoiceItemInputDto> items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
        {
            throw new BusinessException("At least one purchase line is required.");
        }

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        if (productIds.Count != items.Count)
        {
            throw new BusinessException("Duplicate products on the same purchase are not allowed.");
        }

        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lines = new List<PurchaseInvoiceItem>();
        foreach (var input in items)
        {
            if (input.Quantity <= 0)
            {
                throw new BusinessException("Quantity must be greater than zero.");
            }

            if (input.UnitCost < 0)
            {
                throw new BusinessException("Unit cost cannot be negative.");
            }

            if (!products.TryGetValue(input.ProductId, out var product))
            {
                throw new BusinessException($"Product #{input.ProductId} was not found or is inactive.");
            }

            var qty = Math.Round(input.Quantity, 3, MidpointRounding.AwayFromZero);
            var unitCost = PricingCalculator.RoundMoney(input.UnitCost);
            var lineTotal = PricingCalculator.RoundMoney(qty * unitCost);

            lines.Add(new PurchaseInvoiceItem
            {
                ProductId = product.Id,
                ProductNameAr = product.NameAr,
                ProductNameEn = product.NameEn,
                Unit = product.Unit,
                Quantity = qty,
                UnitCost = unitCost,
                LineTotal = lineTotal
            });
        }

        return lines;
    }

    private async Task EnsureSupplierActiveAsync(int supplierId, CancellationToken cancellationToken)
    {
        var exists = await _db.Suppliers.AnyAsync(x => x.Id == supplierId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("Supplier not found or inactive.");
        }
    }

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("User not found or inactive.");
        }
    }

    private async Task<string> ResolveInvoiceNumberAsync(
        string? requested,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var number = requested.Trim();
            var clash = await _db.PurchaseInvoices.AnyAsync(
                x => x.InvoiceNumber == number && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
            if (clash)
            {
                throw new BusinessException("Invoice number is already used.");
            }

            return number;
        }

        // Auto: P-yyyyMMdd-#### based on next id-ish sequence for the day
        var prefix = $"P-{DateTime.UtcNow:yyyyMMdd}-";
        var last = await _db.PurchaseInvoices
            .Where(x => x.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(x => x.InvoiceNumber)
            .Select(x => x.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && last.Length > prefix.Length
            && int.TryParse(last[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{prefix}{next:D4}";
    }

    private static DateTime NormalizeInvoiceDate(DateTime invoiceDate)
    {
        if (invoiceDate == default)
        {
            return DateTime.UtcNow.Date;
        }

        return DateTime.SpecifyKind(invoiceDate.Date, DateTimeKind.Utc);
    }
}
