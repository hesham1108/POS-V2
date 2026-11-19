using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface ITreasuryService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TreasuryListItemDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<TreasuryDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<TreasuryDetailDto> CreateAsync(TreasuryFormDto form, CancellationToken cancellationToken = default);

    Task<TreasuryDetailDto> UpdateAsync(TreasuryFormDto form, CancellationToken cancellationToken = default);

    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task DepositAsync(TreasuryMoveDto dto, int userId, CancellationToken cancellationToken = default);

    Task WithdrawAsync(TreasuryMoveDto dto, int userId, CancellationToken cancellationToken = default);

    Task TransferAsync(TreasuryTransferDto dto, int userId, CancellationToken cancellationToken = default);

    Task<TreasuryStatementDto> GetStatementAsync(
        TreasuryStatementFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>Shift close handover — drawer cash into a treasury (ledger In, source ShiftClose).</summary>
    Task RecordShiftHandoverAsync(
        int treasuryId,
        decimal amount,
        int shiftId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Non-cash sale settlement into the method's default treasury.</summary>
    Task RecordSaleSettlementAsync(
        int treasuryId,
        decimal amount,
        int salesInvoiceId,
        int userId,
        string? note = null,
        CancellationToken cancellationToken = default);

    /// <summary>Purchase payment from a treasury (ledger Out, source Purchase).</summary>
    Task RecordPurchasePaymentAsync(
        int treasuryId,
        decimal amount,
        int purchaseInvoiceId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Incoming cheque collected → treasury In (rule 15).</summary>
    Task RecordChequeCollectedAsync(
        int treasuryId,
        decimal amount,
        int chequeId,
        int userId,
        string? note = null,
        CancellationToken cancellationToken = default);

    /// <summary>Outgoing cheque paid → treasury Out (rule 15).</summary>
    Task RecordChequePaidAsync(
        int treasuryId,
        decimal amount,
        int chequeId,
        int userId,
        string? note = null,
        CancellationToken cancellationToken = default);
}
