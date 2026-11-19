using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class TreasuryIndexViewModel
{
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];
    public bool CanManage { get; set; }
    public bool CanMove { get; set; }
}

public class TreasuryEditViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(120)]
    [Display(Name = "English name")]
    public string NameEn { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    [Display(Name = "Arabic name")]
    public string NameAr { get; set; } = string.Empty;

    [Display(Name = "Type")]
    public TreasuryType Type { get; set; } = TreasuryType.Cash;

    [Display(Name = "Opening balance")]
    [Range(0, 10_000_000)]
    public decimal OpeningBalance { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Default treasury")]
    public bool IsDefault { get; set; }
}

public class TreasuryMoveViewModel
{
    public int TreasuryId { get; set; }
    public string TreasuryName { get; set; } = string.Empty;
    public decimal Balance { get; set; }

    [Display(Name = "Amount")]
    [Range(0.01, 10_000_000)]
    public decimal Amount { get; set; }

    [Required, MaxLength(500)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>"deposit" or "withdraw".</summary>
    public string Mode { get; set; } = "deposit";
}

public class TreasuryTransferViewModel
{
    [Display(Name = "From treasury")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose the source treasury.")]
    public int FromTreasuryId { get; set; }

    [Display(Name = "To treasury")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose the destination treasury.")]
    public int ToTreasuryId { get; set; }

    [Display(Name = "Amount")]
    [Range(0.01, 10_000_000)]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    [Display(Name = "Note (optional)")]
    public string? Note { get; set; }

    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];
}

public class TreasuryStatementViewModel
{
    public TreasuryStatementDto Statement { get; set; } = new();

    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime? FromDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime? ToDate { get; set; }

    public bool Print { get; set; }
}
