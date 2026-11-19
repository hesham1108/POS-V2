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
    public IReadOnlyList<SalesInvoiceListItemDto> Invoices { get; set; } = [];
    public decimal Total => Invoices.Sum(x => x.Total);
}
