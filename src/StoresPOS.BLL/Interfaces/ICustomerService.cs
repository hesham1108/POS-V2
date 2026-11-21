using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

/// <summary>Customers (docs/page-composition.md §18). No customer on an invoice = walk-in ("عميل نقدي").</summary>
public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetListAsync(CustomerListFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<PagedResult<CustomerDto>> GetPageAsync(CustomerListFilterDto filter, ListQuery list, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>POS picker: active customers whose name or phone contains the term.</summary>
    Task<IReadOnlyList<CustomerOptionDto>> SearchAsync(string? term, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>POS picker, paged: active customers whose name or phone contains the term, ordered by name.</summary>
    Task<CustomerOptionPageDto> SearchPageAsync(string? term, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CustomerUpsertDto dto, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(int id, CustomerUpsertDto dto, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
