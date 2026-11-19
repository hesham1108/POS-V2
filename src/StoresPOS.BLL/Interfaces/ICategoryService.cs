using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryTreeNodeDto>> GetTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(CategoryUpsertDto dto, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryUpsertDto dto, CancellationToken cancellationToken = default);
    Task MoveAsync(int id, int? newParentCategoryId, CancellationToken cancellationToken = default);
    Task ReorderAsync(IReadOnlyList<CategoryReorderItemDto> items, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
