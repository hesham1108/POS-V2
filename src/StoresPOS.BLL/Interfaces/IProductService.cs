using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetListAsync(ProductListFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductDto>> GetPageAsync(ProductListFilterDto filter, ListQuery list, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(ProductUpsertDto dto, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(int id, ProductUpsertDto dto, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
