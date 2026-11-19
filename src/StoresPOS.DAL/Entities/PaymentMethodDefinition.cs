namespace StoresPOS.DAL.Entities;

/// <summary>
/// Managed payment method (Phase 11). Seeded rows keep the same ids as the legacy
/// <see cref="PaymentMethod"/> enum so historical invoices stay valid.
/// </summary>
public class PaymentMethodDefinition
{
    public byte Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public PaymentMethodKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Optional default treasury this method settles into (Phase 12).</summary>
    public int? DefaultTreasuryId { get; set; }

    public Treasury? DefaultTreasury { get; set; }
}
