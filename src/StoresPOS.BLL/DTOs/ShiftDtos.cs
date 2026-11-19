using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

/// <summary>Which of the three gate screens to show after login (journeys A / B / C).</summary>
public enum ShiftGateState
{
    /// <summary>A — no open shift: offer "open shift".</summary>
    NoOpenShift = 1,

    /// <summary>B — a shift opened today is open: offer "resume".</summary>
    OpenShift = 2,

    /// <summary>C — a shift opened before today is still open: show summary, resume or close.</summary>
    OldOpenShift = 3
}

public class ShiftGateDto
{
    public ShiftGateState State { get; set; }
    public ShiftSummaryDto? Shift { get; set; }
    public bool IsParticipant { get; set; }
}

public class ShiftParticipantDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime JoinedAtUtc { get; set; }

    // Per-cashier totals — filled once sales (Phase 7–8) and cash-outs (Phase 9) exist.
    public int InvoiceCount { get; set; }
    public decimal CashSales { get; set; }
    public decimal NonCashSales { get; set; }
    public decimal CashOuts { get; set; }
}

public class ShiftSummaryDto
{
    public int Id { get; set; }
    public ShiftStatus Status { get; set; }
    public bool IsOld { get; set; }

    public DateTime OpenedAtUtc { get; set; }
    public string OpenedByDisplayName { get; set; } = string.Empty;
    public decimal OpeningAmount { get; set; }

    // Drawer (cash only — business rule 5)
    public decimal CashSales { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal CashOutsTotal { get; set; }
    public int CashOutsCount { get; set; }
    public decimal ExpectedCash { get; set; }

    // Non-cash — shown, never part of the drawer
    public decimal CardSales { get; set; }
    public decimal InstaPaySales { get; set; }
    public decimal VodafoneCashSales { get; set; }
    public decimal NonCashTotal => CardSales + InstaPaySales + VodafoneCashSales;

    /// <summary>Refunds made back to a non-cash method — recorded only, never part of the drawer.</summary>
    public decimal NonCashRefunds { get; set; }
    public int ReturnsCount { get; set; }

    public int InvoiceCount { get; set; }
    public IReadOnlyList<ShiftParticipantDto> Participants { get; set; } = [];

    /// <summary>Held invoices (F8). They are cancelled when the shift closes.</summary>
    public int HeldInvoiceCount { get; set; }

    /// <summary>Non-empty live carts. Closing waits until each is paid or held.</summary>
    public IReadOnlyList<InProgressSaleDto> InProgressSales { get; set; } = [];

    // Close data (null while open)
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByDisplayName { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? CashDifference { get; set; }
    public ShiftReconciliation? Reconciliation { get; set; }
    public string? CloseNotes { get; set; }
}

public class CloseShiftDto
{
    public int ShiftId { get; set; }

    /// <summary>Optional counted cash. Null/empty = Not Reconciled (business rule 4).</summary>
    public decimal? ActualCash { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Shift history filter "result" (docs/page-composition.md §6.2).</summary>
public enum ShiftResultFilter
{
    Open = 1,
    Matched = 2,
    Surplus = 3,
    Shortage = 4,
    NotReconciled = 5
}

public class ShiftHistoryFilterDto
{
    /// <summary>Shifts opened on/after this instant.</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Shifts opened before this instant.</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>Shifts the user opened, closed or worked in.</summary>
    public int? UserId { get; set; }

    public ShiftResultFilter? Result { get; set; }
    public int Take { get; set; } = 500;
}

public class ShiftHistoryItemDto
{
    public int Id { get; set; }
    public ShiftStatus Status { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string OpenedByDisplayName { get; set; } = string.Empty;
    public string? ClosedByDisplayName { get; set; }
    public IReadOnlyList<string> Participants { get; set; } = [];
    public int InvoiceCount { get; set; }
    public decimal SalesTotal { get; set; }

    /// <summary>Snapshot at close; live figure while the shift is open.</summary>
    public decimal ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? CashDifference { get; set; }
    public ShiftReconciliation? Reconciliation { get; set; }
}

/// <summary>Bridges the history row to the status chip partial.</summary>
public static class ShiftHistoryItemExtensions
{
    public static ShiftSummaryDto ToChip(this ShiftHistoryItemDto item) =>
        new() { Id = item.Id, Status = item.Status, Reconciliation = item.Reconciliation };
}
