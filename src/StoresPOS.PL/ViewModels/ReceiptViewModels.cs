using StoresPOS.BLL.DTOs;
using StoresPOS.PL.Printing;

namespace StoresPOS.PL.ViewModels;

public class SaleReceiptViewModel
{
    public SalesInvoiceDetailsDto Invoice { get; set; } = new();
    public ReceiptFormat Format { get; set; } = null!;
    public string BarcodeSvg { get; set; } = string.Empty;
    public bool AutoPrint { get; set; }
    public IReadOnlyList<SalesReturnListItemDto> Returns { get; set; } = [];
}

public class ShiftReportViewModel
{
    public ShiftSummaryDto Shift { get; set; } = new();
    public ReceiptFormat Format { get; set; } = null!;
    public DateTime PrintedAtUtc { get; set; }
    public bool AutoPrint { get; set; }
}

public class ShiftClosedViewModel
{
    public ShiftSummaryDto Shift { get; set; } = new();
    public bool ReturnToGate { get; set; }
    public bool AutoPrint { get; set; }
}

public class PurchaseA4ViewModel
{
    public PurchaseInvoiceDetailDto Purchase { get; set; } = new();
    public ReceiptFormat Format { get; set; } = null!;
    public bool AutoPrint { get; set; }
}
