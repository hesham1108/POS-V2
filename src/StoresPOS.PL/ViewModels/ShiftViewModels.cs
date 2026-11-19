using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.ViewModels;

public class ShiftGateViewModel
{
    public ShiftGateDto Gate { get; set; } = new();
    public bool CanClose { get; set; }

    [Display(Name = "Opening amount (optional)")]
    [Range(0, 10_000_000, ErrorMessage = "Opening amount must be zero or more.")]
    public decimal? OpeningAmount { get; set; }
}

public class ShiftCurrentViewModel
{
    public ShiftSummaryDto Summary { get; set; } = new();
    public bool IsParticipant { get; set; }
    public bool CanClose { get; set; }
    public bool CanCashOut { get; set; }
    public IReadOnlyList<CashOutListItemDto> CashOuts { get; set; } = [];
}

public class ShiftCloseViewModel
{
    public ShiftSummaryDto Summary { get; set; } = new();

    public int ShiftId { get; set; }

    [Display(Name = "Actual cash in drawer (optional)")]
    [Range(0, 10_000_000, ErrorMessage = "Actual cash must be zero or more.")]
    public decimal? ActualCash { get; set; }

    [Display(Name = "Notes (optional)")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [Display(Name = "Hand over to treasury")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a treasury for the drawer handover.")]
    public int HandoverTreasuryId { get; set; }

    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];

    /// <summary>"gate" when closing an old shift from the gate (journey C) — return there to open a new one.</summary>
    public string? ReturnTo { get; set; }
}
