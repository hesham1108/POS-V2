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
