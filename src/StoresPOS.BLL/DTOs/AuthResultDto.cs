namespace StoresPOS.BLL.DTOs;

public class AuthResultDto
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }

    public static AuthResultDto Fail(string message) => new()
    {
        Succeeded = false,
        ErrorMessage = message
    };
}
