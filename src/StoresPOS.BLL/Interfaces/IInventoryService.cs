using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        StockBalanceFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockBalanceDto>> GetLowStockAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        StockMovementFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<StockMovementDto> AdjustStockAsync(
        StockAdjustmentDto dto,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies purchase receipt: increases stock, updates cost/prices, writes movements.
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplyPurchaseReceiptAsync(
        int purchaseInvoiceId,
        IReadOnlyList<PurchaseStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts stock for a completed sale and writes Sale movements (reference: SalesInvoice).
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplySaleAsync(
        int salesInvoiceId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts returned goods back on the shelf and writes SaleReturn movements (reference: SalesReturn).
    /// Must be called inside the caller's DB transaction when using SQL Server.
    /// </summary>
    Task ApplySaleReturnAsync(
        int salesReturnId,
        IReadOnlyList<SaleStockLineDto> lines,
        int userId,
        CancellationToken cancellationToken = default);
}
