using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class CustomerFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    [Display(Name = "Name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mobile")]
    [StringLength(40)]
    public string? Phone { get; set; }

    [Display(Name = "Address")]
    [StringLength(400)]
    public string? Address { get; set; }

    [Display(Name = "Notes")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Customer type")]
    public CustomerType CustomerType { get; set; } = CustomerType.Retail;

    /// <summary>Price level used when this customer is picked in the POS (separate from the type).</summary>
    [Display(Name = "Default price level")]
    public SaleType DefaultSaleType { get; set; } = SaleType.Retail;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class CustomerIndexViewModel
{
    public string? Search { get; set; }
    public CustomerType? CustomerType { get; set; }
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<CustomerDto> Customers { get; set; } = [];

    /// <summary>Paging/sort state of the list (Phase 20).</summary>
    public IPagedResult Page { get; set; } = new PagedResult<object>();
}

public class CustomerDetailsViewModel
{
    public CustomerDto Customer { get; set; } = null!;
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices { get; set; } = [];
    public IReadOnlyList<SalesReturnListItemDto> Returns { get; set; } = [];
    public IReadOnlyList<ChequeListItemDto> Cheques { get; set; } = [];
    public decimal OutstandingCheques { get; set; }
}
