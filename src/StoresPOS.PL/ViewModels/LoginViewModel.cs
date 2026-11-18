using System.ComponentModel.DataAnnotations;

namespace StoresPOS.PL.ViewModels;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
