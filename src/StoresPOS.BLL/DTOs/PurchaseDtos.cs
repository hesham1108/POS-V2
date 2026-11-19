using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class PurchaseInvoiceListItemDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierNameEn { get; set; } = string.Empty;
    public string SupplierNameAr { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public PurchaseInvoiceStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}

public class PurchaseInvoiceItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseInvoiceDetailDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierNameEn { get; set; } = string.Empty;
    public string SupplierNameAr { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public PurchaseInvoiceStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? ConfirmedByUsername { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public int? PaidFromTreasuryId { get; set; }
    public string? PaidFromTreasuryNameEn { get; set; }
    public string? PaidFromTreasuryNameAr { get; set; }
    public int? ReceivedIntoWarehouseId { get; set; }
    public string? ReceivedIntoWarehouseNameEn { get; set; }
    public string? ReceivedIntoWarehouseNameAr { get; set; }
    public IReadOnlyList<PurchaseInvoiceItemDto> Items { get; set; } = [];
}

public class PurchaseInvoiceItemInputDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseInvoiceUpsertDto
{
    public string? InvoiceNumber { get; set; }
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PurchaseInvoiceItemInputDto> Items { get; set; } = [];
}

public class PurchaseInvoiceListFilterDto
{
    public string? Search { get; set; }
    public int? SupplierId { get; set; }
    public PurchaseInvoiceStatus? Status { get; set; }
}
