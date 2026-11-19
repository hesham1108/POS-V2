using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetListAsync(SupplierListFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(SupplierUpsertDto dto, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateAsync(int id, SupplierUpsertDto dto, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
