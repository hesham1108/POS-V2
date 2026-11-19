using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class ReportOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Shared by every report: store-local dates (end date inclusive) + filter option lists.</summary>
public abstract class ReportPageViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<ReportOption> Users { get; set; } = [];
}

public class SalesReportViewModel : ReportPageViewModel
{
    public int? CashierId { get; set; }
    public PaymentMethod? Method { get; set; }
    public SaleType? SaleType { get; set; }
    public int? CategoryId { get; set; }
    public IReadOnlyList<ReportOption> Categories { get; set; } = [];
    public SalesReportDto Report { get; set; } = new();
}

public class PurchasesReportViewModel : ReportPageViewModel
{
    public int? SupplierId { get; set; }
    public IReadOnlyList<ReportOption> Suppliers { get; set; } = [];
    public PurchasesReportDto Report { get; set; } = new();
}

public class InventoryReportViewModel : ReportPageViewModel
{
    public int? CategoryId { get; set; }
    public bool LowOnly { get; set; }
    public IReadOnlyList<ReportOption> Categories { get; set; } = [];
    public InventoryReportDto Report { get; set; } = new();
}

public class ShiftsReportViewModel : ReportPageViewModel
{
    public int? UserId { get; set; }
    public ShiftResultFilter? Result { get; set; }
    public ShiftsReportDto Report { get; set; } = new();
}

public class CashOutsReportViewModel : ReportPageViewModel
{
    public int? UserId { get; set; }
    public CashOutsReportDto Report { get; set; } = new();
}

public class ShiftHistoryViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? UserId { get; set; }
    public ShiftResultFilter? Result { get; set; }
    public IReadOnlyList<ReportOption> Users { get; set; } = [];
    public IReadOnlyList<ShiftHistoryItemDto> Shifts { get; set; } = [];
}

public class ShiftDetailsViewModel
{
    public ShiftSummaryDto Summary { get; set; } = null!;
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices { get; set; } = [];
    public IReadOnlyList<CashOutListItemDto> CashOuts { get; set; } = [];
    public IReadOnlyList<SalesReturnListItemDto> Returns { get; set; } = [];
}
