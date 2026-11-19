namespace StoresPOS.DAL.Entities;

/// <summary>
/// A named customer. The walk-in customer ("عميل نقدي") is not a row: an invoice with no customer is a walk-in sale.
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    /// <summary>Picking this customer in the POS switches the invoice to this sale type.</summary>
    public SaleType DefaultSaleType { get; set; } = SaleType.Retail;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
