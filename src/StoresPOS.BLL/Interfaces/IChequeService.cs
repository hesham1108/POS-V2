using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Interfaces;

public interface IChequeService
{
    Task<ChequeListResultDto> ListAsync(
        ChequeFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<ChequeDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ChequeDetailDto> CreateAsync(
        ChequeFormDto form,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an incoming Pending cheque linked to a completed sale (POS Cheque pay).</summary>
    Task<ChequeDetailDto> CreateForSaleAsync(
        ChequeFormDto form,
        int salesInvoiceId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an outgoing Pending cheque linked to a confirmed purchase.</summary>
    Task<ChequeDetailDto> CreateForPurchaseAsync(
        ChequeFormDto form,
        int purchaseInvoiceId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ChequeDetailDto> ChangeStatusAsync(
        ChequeStatusChangeDto dto,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ChequeDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<ChequesReportDto> GetReportAsync(
        ChequesReportFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChequeListItemDto>> ListForCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChequeListItemDto>> ListForSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default);

    Task<decimal> OutstandingTotalForCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<decimal> OutstandingTotalForSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default);

    bool IsTerminal(ChequeStatus status);
}
