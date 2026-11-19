using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IWarehouseService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    /// <summary>Warehouse POS sells from / returns restock into (setting or IsDefault).</summary>
    Task<int> GetSellingWarehouseIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseListItemDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<WarehouseDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<WarehouseDetailDto> CreateAsync(WarehouseFormDto form, CancellationToken cancellationToken = default);

    Task<WarehouseDetailDto> UpdateAsync(WarehouseFormDto form, CancellationToken cancellationToken = default);

    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseStockRowDto>> GetStockAsync(
        int warehouseId,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<StockTransferDetailDto> CreateTransferAsync(
        StockTransferCreateDto dto,
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockTransferListItemDto>> ListTransfersAsync(
        int? warehouseId = null,
        CancellationToken cancellationToken = default);

    Task<StockTransferDetailDto?> GetTransferAsync(int id, CancellationToken cancellationToken = default);
}
