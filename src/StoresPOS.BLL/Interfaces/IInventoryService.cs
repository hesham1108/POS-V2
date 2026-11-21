using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IInventoryService
{
    Task EnsureWarehouseInfrastructureAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        StockBalanceFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockBalanceDto>> GetLowStockAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        StockMovementFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockMovementDto>> GetMovementPageAsync(StockMovementFilterDto filter, ListQuery list, CancellationToken cancellationToken = default);

    Task<StockMovementDto> AdjustStockAsync(
        StockAdjustmentDto dto,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies purchase receipt into a warehouse: increases stock, updates cost/prices, writes movements.
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplyPurchaseReceiptAsync(
        int purchaseInvoiceId,
        int warehouseId,
        IReadOnlyList<PurchaseStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts stock for a completed sale from a warehouse and writes Sale movements.
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplySaleAsync(
        int salesInvoiceId,
        int warehouseId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts returned goods back into a warehouse and writes SaleReturn movements.
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplySaleReturnAsync(
        int salesReturnId,
        int warehouseId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves stock between warehouses (two movements per line, same transfer document).
    /// Product total stock is unchanged. Caller supplies the transfer id after saving the header.
    /// </summary>
    Task ApplyTransferAsync(
        int stockTransferId,
        int fromWarehouseId,
        int toWarehouseId,
        IReadOnlyList<TransferStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetWarehouseQuantityAsync(
        int warehouseId,
        int productId,
        CancellationToken cancellationToken = default);
}
