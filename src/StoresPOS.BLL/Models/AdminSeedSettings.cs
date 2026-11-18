namespace StoresPOS.BLL.Models;

public class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "System Admin";
}
