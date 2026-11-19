using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class CashOutIndexViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? ShiftId { get; set; }
    public int? UserId { get; set; }

    /// <summary>False for cashiers: the list is limited to the shift they are working in.</summary>
    public bool CanSeeAll { get; set; }
    public bool CanCreate { get; set; }
    public bool InShift { get; set; }
    public IReadOnlyList<CashOutListItemDto> Items { get; set; } = [];
    public IReadOnlyList<(int Id, string Name)> Users { get; set; } = [];
    public decimal Total => Items.Sum(x => x.Amount);
}

public class ReturnIndexViewModel
{
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PaymentMethod? Method { get; set; }
    public int? PrintId { get; set; }
    public bool CanSeeAll { get; set; }
    public bool CanCreate { get; set; }
    public IReadOnlyList<SalesReturnListItemDto> Items { get; set; } = [];
    public decimal Total => Items.Sum(x => x.TotalAmount);
}

public class ReturnLineInput
{
    public int SalesInvoiceItemId { get; set; }

    [Range(0, 100000)]
    public decimal? Quantity { get; set; }
}

public class ReturnCreateViewModel
{
    /// <summary>Null while the user still has to scan / type the invoice number.</summary>
    public ReturnableInvoiceDto? Invoice { get; set; }

    public string? Number { get; set; }

    public int InvoiceId { get; set; }
    public List<ReturnLineInput> Lines { get; set; } = [];
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(300)]
    public string? Reason { get; set; }
}

public class ReturnReceiptViewModel
{
    public SalesReturnDetailsDto Return { get; set; } = new();
    public StoresPOS.PL.Printing.ReceiptFormat Format { get; set; } = null!;
    public string BarcodeSvg { get; set; } = string.Empty;
    public bool AutoPrint { get; set; }
}
