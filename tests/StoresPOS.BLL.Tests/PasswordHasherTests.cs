using StoresPOS.BLL.Services;

namespace StoresPOS.BLL.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ThenVerify_Succeeds()
    {
        var (hash, salt) = _hasher.HashPassword("Admin@12345");

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));
        Assert.True(_hasher.VerifyPassword("Admin@12345", hash, salt));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_Fails()
    {
        var (hash, salt) = _hasher.HashPassword("Admin@12345");
        Assert.False(_hasher.VerifyPassword("wrong-password", hash, salt));
    }
}
