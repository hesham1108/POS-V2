using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

public interface IShiftService
{
    /// <summary>Decides which gate screen (A/B/C) the user sees after login.</summary>
    Task<ShiftGateDto> GetGateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Journey A. Opening amount optional (null = 0). Opener becomes the first participant.</summary>
    Task<ShiftSummaryDto> OpenAsync(decimal? openingAmount, int userId, CancellationToken cancellationToken = default);

    /// <summary>Journeys B/C. Joins the open shift as a participant (idempotent).</summary>
    Task<ShiftSummaryDto> ResumeAsync(int userId, CancellationToken cancellationToken = default);

    Task<ShiftSummaryDto?> GetOpenShiftAsync(CancellationToken cancellationToken = default);

    Task<ShiftSummaryDto?> GetSummaryAsync(int shiftId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open shift id when <paramref name="userId"/> participates in it; otherwise null.
    /// POS (Phase 7) uses this to attribute sales and to send the user back to the gate.
    /// </summary>
    Task<int?> GetActiveShiftIdForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Shift history (newest first) with drawer results — Admin/Manager.</summary>
    Task<IReadOnlyList<ShiftHistoryItemDto>> GetHistoryAsync(ShiftHistoryFilterDto? filter = null, CancellationToken cancellationToken = default);

    /// <summary>Journey H. Optional cash count; blank = Not Reconciled.</summary>
    Task<ShiftSummaryDto> CloseAsync(CloseShiftDto dto, int userId, CancellationToken cancellationToken = default);
}
