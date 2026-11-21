using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class SalesIndexViewModel
{
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PaymentMethod? Method { get; set; }
    public int? ShiftId { get; set; }
    public int? CashierId { get; set; }
    public int? ProductId { get; set; }
    public PagedResult<SalesInvoiceListItemDto> Page { get; set; } = new();
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices => Page.Items;

    /// <summary>Total of every matching invoice (all pages).</summary>
    public decimal Total => Page.Sum ?? 0m;
}
