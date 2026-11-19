using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class ChequeIndexViewModel
{
    public ChequeDirection Direction { get; set; } = ChequeDirection.In;
    public ChequeStatus? Status { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public bool OverdueOnly { get; set; }
    public string? Search { get; set; }
    public ChequeListResultDto Result { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanChangeStatus { get; set; }
    public bool Print { get; set; }
}

public class ChequeEditViewModel
{
    [Required, MaxLength(60)]
    [Display(Name = "Cheque number")]
    public string Number { get; set; } = string.Empty;

    [MaxLength(120)]
    [Display(Name = "Bank (optional)")]
    public string? Bank { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Due date")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

    [Range(0.01, 10_000_000)]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Display(Name = "Direction")]
    public ChequeDirection Direction { get; set; } = ChequeDirection.In;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOptionViewModel> CustomerOptions { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> SupplierOptions { get; set; } = [];
}

public class SelectOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ChequeDetailsViewModel
{
    public ChequeDetailDto Cheque { get; set; } = new();
    public bool CanChangeStatus { get; set; }
    public IReadOnlyList<TreasuryListItemDto> Treasuries { get; set; } = [];
}

public class ChequeStatusFormViewModel
{
    public int ChequeId { get; set; }
    public ChequeStatus NewStatus { get; set; }

    [Display(Name = "Treasury")]
    public int? TreasuryId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Note")]
    public string? Note { get; set; }
}
