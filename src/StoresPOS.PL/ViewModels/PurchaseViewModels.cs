using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class PurchaseLineFormViewModel
{
    [Required]
    [Display(Name = "Product")]
    public int ProductId { get; set; }

    [Range(0.001, double.MaxValue)]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue)]
    [Display(Name = "Unit cost")]
    public decimal UnitCost { get; set; }
}

public class PurchaseFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Invoice number")]
    [StringLength(40)]
    public string? InvoiceNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Supplier is required.")]
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Invoice date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Display(Name = "Notes")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<PurchaseLineFormViewModel> Items { get; set; } = [new()];

    public bool ConfirmOnSave { get; set; }

    [Display(Name = "Pay from treasury")]
    public int? PaidFromTreasuryId { get; set; }

    public IReadOnlyList<SupplierOptionViewModel> SupplierOptions { get; set; } = [];
    public IReadOnlyList<ProductOptionViewModel> ProductOptions { get; set; } = [];
    public IReadOnlyList<TreasuryListItemDto> TreasuryOptions { get; set; } = [];
}

public class ProductOptionViewModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public string Unit { get; set; } = "pcs";
}

public class PurchaseIndexViewModel
{
    public string? Search { get; set; }
    public int? SupplierId { get; set; }
    public PurchaseInvoiceStatus? Status { get; set; }
    public IReadOnlyList<SupplierOptionViewModel> SupplierOptions { get; set; } = [];
    public IReadOnlyList<PurchaseListItemViewModel> Invoices { get; set; } = [];
}

public class PurchaseListItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public PurchaseInvoiceStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public int LineCount { get; set; }
}

public class PurchaseDetailsViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public PurchaseInvoiceStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? ConfirmedByUsername { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public int? PaidFromTreasuryId { get; set; }
    public string? PaidFromTreasuryName { get; set; }
    public IReadOnlyList<TreasuryListItemDto> TreasuryOptions { get; set; } = [];
    public IReadOnlyList<PurchaseLineDetailsViewModel> Items { get; set; } = [];
}

public class PurchaseLineDetailsViewModel
{
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
