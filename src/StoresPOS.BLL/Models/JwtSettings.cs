namespace StoresPOS.BLL.Models;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "StoresPOS";
    public string Audience { get; set; } = "StoresPOS";
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 480;
}
