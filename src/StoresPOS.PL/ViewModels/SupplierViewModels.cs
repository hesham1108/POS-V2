using StoresPOS.BLL.DTOs;
using System.ComponentModel.DataAnnotations;

namespace StoresPOS.PL.ViewModels;

public class SupplierFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Arabic name")]
    [StringLength(200)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [Display(Name = "English name")]
    [StringLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Display(Name = "Phone")]
    [StringLength(40)]
    public string? Phone { get; set; }

    [Display(Name = "Email")]
    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [Display(Name = "Address")]
    [StringLength(400)]
    public string? Address { get; set; }

    [Display(Name = "Notes")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class SupplierListItemViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int ConfirmedPurchaseCount { get; set; }
    public decimal TotalPurchasesAmount { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierIndexViewModel
{
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<SupplierListItemViewModel> Suppliers { get; set; } = [];

    /// <summary>Paging/sort state of the list (Phase 20).</summary>
    public IPagedResult Page { get; set; } = new PagedResult<object>();
}

public class SupplierOptionViewModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
