using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwt()
    {
        await using var db = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = CreateJwtService();
        var auth = new AuthService(db, hasher, jwt, Options.Create(new AdminSeedSettings
        {
            Username = "admin",
            Password = "Admin@12345",
            DisplayName = "System Admin"
        }));

        await auth.EnsureSeedDataAsync();
        var result = await auth.LoginAsync(new LoginRequestDto
        {
            Username = "admin",
            Password = "Admin@12345"
        });

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(AppRoles.Admin, result.Role);
        Assert.True(jwt.ValidateToken(result.AccessToken!, out var principal));
        Assert.True(principal!.IsInRole(AppRoles.Admin));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Fails()
    {
        await using var db = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = CreateJwtService();
        var auth = new AuthService(db, hasher, jwt, Options.Create(new AdminSeedSettings
        {
            Username = "admin",
            Password = "Admin@12345",
            DisplayName = "System Admin"
        }));

        await auth.EnsureSeedDataAsync();
        var result = await auth.LoginAsync(new LoginRequestDto
        {
            Username = "admin",
            Password = "bad-password"
        });

        Assert.False(result.Succeeded);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_Fails()
    {
        var settings = new JwtSettings
        {
            Issuer = "StoresPOS",
            Audience = "StoresPOS",
            SecretKey = "StoresPOS-Dev-Only-Secret-Key-Change-In-Production!",
            ExpirationMinutes = 60
        };
        var jwt = new JwtTokenService(Options.Create(settings));

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(settings.SecretKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var expiredToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin")],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: credentials);
        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(expiredToken);

        Assert.False(jwt.ValidateToken(tokenString, out _));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static JwtTokenService CreateJwtService()
    {
        return new JwtTokenService(Options.Create(new JwtSettings
        {
            Issuer = "StoresPOS",
            Audience = "StoresPOS",
            SecretKey = "StoresPOS-Dev-Only-Secret-Key-Change-In-Production!",
            ExpirationMinutes = 60
        }));
    }
}
