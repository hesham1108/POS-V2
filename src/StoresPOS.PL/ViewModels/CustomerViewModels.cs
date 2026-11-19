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

    [Display(Name = "Default sale type")]
    public SaleType DefaultSaleType { get; set; } = SaleType.Retail;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class CustomerIndexViewModel
{
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<CustomerDto> Customers { get; set; } = [];
}

public class CustomerDetailsViewModel
{
    public CustomerDto Customer { get; set; } = null!;
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices { get; set; } = [];
    public IReadOnlyList<SalesReturnListItemDto> Returns { get; set; } = [];
    public IReadOnlyList<ChequeListItemDto> Cheques { get; set; } = [];
    public decimal OutstandingCheques { get; set; }
}
