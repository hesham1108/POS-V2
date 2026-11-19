using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Interfaces;

/// <summary>
/// POS cart (journey D) persisted as the cashier's Draft sales invoice, plus hold/resume and completion.
/// Every cart method requires the user to be a participant of the open shift.
/// </summary>
public interface ISalesService
{
    Task<IReadOnlyList<PosCategoryDto>> GetPosCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(PosSearchDto search, CancellationToken cancellationToken = default);

    Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Adds the active product whose barcode matches exactly; throws when none.</summary>
    Task<CartDto> ScanAsync(int userId, string barcode, CancellationToken cancellationToken = default);

    Task<CartDto> AddProductAsync(int userId, int productId, decimal quantity = 1m, CancellationToken cancellationToken = default);
    Task<CartDto> SetQuantityAsync(int userId, int lineId, decimal quantity, CancellationToken cancellationToken = default);
    Task<CartDto> ChangeQuantityAsync(int userId, int lineId, decimal delta, CancellationToken cancellationToken = default);
    Task<CartDto> RemoveLineAsync(int userId, int lineId, CancellationToken cancellationToken = default);
    Task<CartDto> SetSaleTypeAsync(int userId, SaleType saleType, CancellationToken cancellationToken = default);

    /// <summary>Invoice-line-only price change (rule 6). Requires Sales.PriceOverride.</summary>
    Task<CartDto> OverridePriceAsync(int userId, int lineId, decimal unitPrice, CancellationToken cancellationToken = default);

    /// <summary>Invoice discount (amount or %). Requires Sales.Discount. Zero clears it.</summary>
    Task<CartDto> SetDiscountAsync(int userId, DiscountDto discount, CancellationToken cancellationToken = default);

    Task<CartDto> ClearAsync(int userId, CancellationToken cancellationToken = default);

    Task<CartDto> HoldAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeldInvoiceDto>> GetHeldAsync(int userId, CancellationToken cancellationToken = default);
    Task<CartDto> ResumeHeldAsync(int userId, int invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds another cashier's in-progress cart so the shift can close. Requires Shifts.Close.
    /// </summary>
    Task HoldForCashierAsync(int actorUserId, int invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Pay (F9): saves on shift + cashier, deducts stock, assigns the invoice number. One method per invoice.</summary>
    Task<CompletedSaleDto> CompleteAsync(int userId, PaymentDto payment, CancellationToken cancellationToken = default);

    /// <summary>The cashier's most recent completed invoice in their open shift ("reprint last").</summary>
    Task<int?> GetLastInvoiceIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceListItemDto>> GetInvoicesAsync(SalesInvoiceFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<SalesInvoiceDetailsDto?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default);
}
