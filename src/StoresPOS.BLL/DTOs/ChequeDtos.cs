using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class ChequeListItemDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public ChequeDirection Direction { get; set; }
    public ChequeStatus Status { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? SalesInvoiceId { get; set; }
    public string? SalesInvoiceNumber { get; set; }
    public int? PurchaseInvoiceId { get; set; }
    public string? PurchaseInvoiceNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsOverdue { get; set; }
}

public class ChequeDetailDto : ChequeListItemDto
{
    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public int? SettledTreasuryId { get; set; }
    public string? SettledTreasuryName { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public IReadOnlyList<ChequeStatusHistoryDto> History { get; set; } = [];
}

public class ChequeStatusHistoryDto
{
    public ChequeStatus Status { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? TreasuryName { get; set; }
    public string? Note { get; set; }
}

public class ChequeFormDto
{
    public string Number { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public ChequeDirection Direction { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string? Notes { get; set; }
    public int? SalesInvoiceId { get; set; }
    public int? PurchaseInvoiceId { get; set; }
}

public class ChequeFilterDto
{
    public ChequeDirection? Direction { get; set; }
    public ChequeStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public bool OverdueOnly { get; set; }
    public string? Search { get; set; }
    public int Take { get; set; } = 500;
}

public class ChequeListResultDto
{
    public IReadOnlyList<ChequeListItemDto> Items { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingCount { get; set; }
    public int OverdueCount { get; set; }

    /// <summary>Paging info when the list was loaded one page at a time (Phase 20); null = every row.</summary>
    public PagedResult<ChequeListItemDto>? Page { get; set; }
}

public class ChequeStatusChangeDto
{
    public int ChequeId { get; set; }
    public ChequeStatus NewStatus { get; set; }
    public int? TreasuryId { get; set; }
    public string? Note { get; set; }
}

public class ChequeDashboardDto
{
    public int IncomingDueToday { get; set; }
    public int IncomingDueThisWeek { get; set; }
    public int IncomingOverdue { get; set; }
    public decimal IncomingOverdueAmount { get; set; }
    public int OutgoingDueToday { get; set; }
    public int OutgoingDueThisWeek { get; set; }
    public int OutgoingOverdue { get; set; }
    public decimal OutgoingOverdueAmount { get; set; }
}

public class ChequesReportFilterDto
{
    public ChequeDirection? Direction { get; set; }
    public ChequeStatus? Status { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
}

public class ChequesReportDto
{
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<NamedAmountDto> ByStatus { get; set; } = [];
    public IReadOnlyList<ChequeListItemDto> Rows { get; set; } = [];
}
