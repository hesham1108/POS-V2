using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public SaleType DefaultSaleType { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public int InvoiceCount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal ReturnedTotal { get; set; }
    public decimal NetTotal => SalesTotal - ReturnedTotal;
    public DateTime? LastPurchaseAtUtc { get; set; }
}

public class CustomerUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public SaleType DefaultSaleType { get; set; } = SaleType.Retail;
    public bool IsActive { get; set; } = true;
}

public class CustomerListFilterDto
{
    public string? Search { get; set; }

    public bool IncludeInactive { get; set; }
    public int Take { get; set; } = 500;
}

/// <summary>Short row for the POS customer picker (F4).</summary>
public class CustomerOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public SaleType DefaultSaleType { get; set; }
}
