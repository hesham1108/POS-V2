using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _db;

    public SupplierService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SupplierDto>> GetListAsync(
        SupplierListFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SupplierListFilterDto();
        var query = _db.Suppliers.AsNoTracking().AsQueryable();

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }
        else
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.NameAr.Contains(term)
                || x.NameEn.Contains(term)
                || (x.Phone != null && x.Phone.Contains(term))
                || (x.Email != null && x.Email.Contains(term)));
        }

        return await query
            .OrderBy(x => x.NameEn)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Phone = x.Phone,
                Email = x.Email,
                Address = x.Address,
                Notes = x.Notes,
                IsActive = x.IsActive,
                ConfirmedPurchaseCount = x.PurchaseInvoices.Count(p => p.Status == PurchaseInvoiceStatus.Confirmed),
                TotalPurchasesAmount = x.PurchaseInvoices
                    .Where(p => p.Status == PurchaseInvoiceStatus.Confirmed)
                    .Sum(p => (decimal?)p.SubTotal) ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Suppliers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                Phone = x.Phone,
                Email = x.Email,
                Address = x.Address,
                Notes = x.Notes,
                IsActive = x.IsActive,
                ConfirmedPurchaseCount = x.PurchaseInvoices.Count(p => p.Status == PurchaseInvoiceStatus.Confirmed),
                TotalPurchasesAmount = x.PurchaseInvoices
                    .Where(p => p.Status == PurchaseInvoiceStatus.Confirmed)
                    .Sum(p => (decimal?)p.SubTotal) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(SupplierUpsertDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);

        var entity = new Supplier
        {
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            Phone = NormalizeOptional(dto.Phone),
            Email = NormalizeOptional(dto.Email),
            Address = NormalizeOptional(dto.Address),
            Notes = NormalizeOptional(dto.Notes),
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<SupplierDto> UpdateAsync(int id, SupplierUpsertDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);

        var entity = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Supplier not found.");

        entity.NameAr = dto.NameAr.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.Phone = NormalizeOptional(dto.Phone);
        entity.Email = NormalizeOptional(dto.Email);
        entity.Address = NormalizeOptional(dto.Address);
        entity.Notes = NormalizeOptional(dto.Notes);
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException("Supplier not found.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(SupplierUpsertDto dto)
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
