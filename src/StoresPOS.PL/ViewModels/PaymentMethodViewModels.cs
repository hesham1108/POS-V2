using System.ComponentModel.DataAnnotations;
using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.ViewModels;

public class PaymentMethodEditViewModel
{
    public byte? Id { get; set; }

    [Required, MaxLength(80)]
    [Display(Name = "English name")]
    public string NameEn { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [Display(Name = "Arabic name")]
    public string NameAr { get; set; } = string.Empty;

    [Display(Name = "Kind")]
    public PaymentMethodKind Kind { get; set; } = PaymentMethodKind.Card;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Default treasury")]
    public int? DefaultTreasuryId { get; set; }

    public IReadOnlyList<TreasuryListItemDto> TreasuryOptions { get; set; } = [];
}
