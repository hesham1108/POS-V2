using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IPurchaseService
{
    Task<IReadOnlyList<PurchaseInvoiceListItemDto>> GetListAsync(
        PurchaseInvoiceListFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDetailDto> CreateDraftAsync(
        PurchaseInvoiceUpsertDto dto,
        int userId,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDetailDto> UpdateDraftAsync(
        int id,
        PurchaseInvoiceUpsertDto dto,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDetailDto> ConfirmAsync(
        int id,
        int userId,
        int paidFromTreasuryId,
        CancellationToken cancellationToken = default);

    /// <summary>Create draft and confirm in one transaction.</summary>
    Task<PurchaseInvoiceDetailDto> CreateAndConfirmAsync(
        PurchaseInvoiceUpsertDto dto,
        int userId,
        int paidFromTreasuryId,
        CancellationToken cancellationToken = default);
}
