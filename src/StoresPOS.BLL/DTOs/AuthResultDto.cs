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

    /// <summary>Minutes until a locked account opens again (Phase 21); null when not locked.</summary>
    public int? LockedMinutes { get; set; }

    /// <summary>The user must choose a new password before using the app.</summary>
    public bool MustChangePassword { get; set; }

    public static AuthResultDto Fail(string message) => new()
    {
        Succeeded = false,
        ErrorMessage = message
    };
}
