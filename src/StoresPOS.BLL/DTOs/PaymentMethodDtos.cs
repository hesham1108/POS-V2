using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class PaymentMethodDto
{
    public byte Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public PaymentMethodKind Kind { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int? DefaultTreasuryId { get; set; }

    public bool IsCash => Kind == PaymentMethodKind.Cash;

    public string NameFor(string language) =>
        language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? NameAr : NameEn;
}

public class PaymentMethodFormDto
{
    public byte? Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public PaymentMethodKind Kind { get; set; } = PaymentMethodKind.Card;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public int? DefaultTreasuryId { get; set; }
}

public class PaymentMethodReorderItemDto
{
    public byte Id { get; set; }
    public int SortOrder { get; set; }
}
