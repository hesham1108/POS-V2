namespace StoresPOS.BLL.DTOs;

public class SupplierDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int ConfirmedPurchaseCount { get; set; }
    public decimal TotalPurchasesAmount { get; set; }
}

public class SupplierUpsertDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierListFilterDto
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
}
