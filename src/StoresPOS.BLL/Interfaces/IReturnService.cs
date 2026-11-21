using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

/// <summary>Journey G: customer returns against a completed sales invoice, at the original prices.</summary>
public interface IReturnService
{
    /// <summary>Completed invoice id for an exact invoice number (scanned from the receipt barcode).</summary>
    Task<int?> FindInvoiceIdAsync(string invoiceNumber, CancellationToken cancellationToken = default);

    Task<ReturnableInvoiceDto?> GetReturnableAsync(int invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the return, puts stock back and — for a cash refund — takes the money out of the open shift's drawer.
    /// Requires Returns.Create (checked in the database).
    /// </summary>
    Task<SalesReturnDetailsDto> CreateAsync(CreateReturnDto dto, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReturnListItemDto>> GetListAsync(SalesReturnFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<PagedResult<SalesReturnListItemDto>> GetPageAsync(SalesReturnFilterDto filter, ListQuery list, CancellationToken cancellationToken = default);

    Task<SalesReturnDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default);
}
