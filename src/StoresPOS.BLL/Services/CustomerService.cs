using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class CustomerService : ICustomerService
{
    public const string MsgNameRequired = "Customer name is required.";
    public const string MsgNotFound = "Customer not found.";
    public const string MsgPhoneTaken = "Another customer already uses the phone {0}.";

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public CustomerService(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetListAsync(
        CustomerListFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new CustomerListFilterDto();
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!filter.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Phone != null && x.Phone.Contains(term)));
        }

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 2000);
        return await Project(query.OrderBy(x => x.Name).Take(take)).ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(_db.Customers.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerOptionDto>> SearchAsync(
        string? term,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Customers.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(x => x.Name.Contains(t) || (x.Phone != null && x.Phone.Contains(t)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new CustomerOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                DefaultSaleType = x.DefaultSaleType
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto> CreateAsync(CustomerUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var phone = await ValidateAsync(dto, null, cancellationToken);
        var entity = new Customer
        {
            Name = dto.Name.Trim(),
            Phone = phone,
            Address = NormalizeOptional(dto.Address),
            Notes = NormalizeOptional(dto.Notes),
            DefaultSaleType = Enum.IsDefined(dto.DefaultSaleType) ? dto.DefaultSaleType : SaleType.Retail,
            IsActive = dto.IsActive,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        var phone = await ValidateAsync(dto, id, cancellationToken);
        entity.Name = dto.Name.Trim();
        entity.Phone = phone;
        entity.Address = NormalizeOptional(dto.Address);
        entity.Notes = NormalizeOptional(dto.Notes);
        entity.DefaultSaleType = Enum.IsDefined(dto.DefaultSaleType) ? dto.DefaultSaleType : SaleType.Retail;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        entity.IsActive = false;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CustomerDto> Project(IQueryable<Customer> query) =>
        query.Select(x => new CustomerDto
        {
            Id = x.Id,
            Name = x.Name,
            Phone = x.Phone,
            Address = x.Address,
            Notes = x.Notes,
            DefaultSaleType = x.DefaultSaleType,
            IsActive = x.IsActive,
            CreatedAtUtc = x.CreatedAtUtc,
            InvoiceCount = _db.SalesInvoices.Count(s => s.CustomerId == x.Id && s.Status == SalesInvoiceStatus.Completed),
            SalesTotal = _db.SalesInvoices
                .Where(s => s.CustomerId == x.Id && s.Status == SalesInvoiceStatus.Completed)
                .Sum(s => (decimal?)s.Total) ?? 0m,
            ReturnedTotal = _db.SalesReturns
                .Where(r => r.OriginalSalesInvoice.CustomerId == x.Id)
                .Sum(r => (decimal?)r.TotalAmount) ?? 0m,
            LastPurchaseAtUtc = _db.SalesInvoices
                .Where(s => s.CustomerId == x.Id && s.Status == SalesInvoiceStatus.Completed)
                .Max(s => s.CompletedAtUtc)
        });

    /// <summary>Returns the normalized phone. A phone, when given, identifies one active customer.</summary>
    private async Task<string?> ValidateAsync(CustomerUpsertDto dto, int? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new BusinessException(MsgNameRequired);
        }

        var phone = NormalizeOptional(dto.Phone);
        if (phone is not null
            && await _db.Customers.AnyAsync(x => x.Phone == phone && x.IsActive && x.Id != id, cancellationToken))
        {
            throw new BusinessException(MsgPhoneTaken, phone);
        }

        return phone;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
