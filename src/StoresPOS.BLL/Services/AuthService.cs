using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AdminSeedSettings _adminSeed;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<AdminSeedSettings> adminSeedOptions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _adminSeed = adminSeedOptions.Value;
    }

    public async Task<AuthResultDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResultDto.Fail("Username and password are required.");
        }

        var username = request.Username.Trim();
        var user = await _db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AuthResultDto.Fail("Invalid username or password.");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return AuthResultDto.Fail("Invalid username or password.");
        }

        var (token, expiresAt) = _jwtTokenService.CreateToken(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            Succeeded = true,
            AccessToken = token,
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role.Name
        };
    }

    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in AppRoles.All)
        {
            var exists = await _db.Roles.AnyAsync(x => x.Name == roleName, cancellationToken);
            if (!exists)
            {
                _db.Roles.Add(new Role
                {
                    Name = roleName,
                    Description = $"{roleName} role"
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (await _db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_adminSeed.Password) || _adminSeed.Password.Length < 8)
        {
            throw new InvalidOperationException(
                "AdminSeed:Password must be configured and at least 8 characters for first-run setup.");
        }

        var adminRole = await _db.Roles
            .SingleAsync(x => x.Name == AppRoles.Admin, cancellationToken);

        var (hash, salt) = _passwordHasher.HashPassword(_adminSeed.Password);
        _db.Users.Add(new User
        {
            Username = string.IsNullOrWhiteSpace(_adminSeed.Username) ? "admin" : _adminSeed.Username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(_adminSeed.DisplayName) ? "System Admin" : _adminSeed.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleId = adminRole.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
