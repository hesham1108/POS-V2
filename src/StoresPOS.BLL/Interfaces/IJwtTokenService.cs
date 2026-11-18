using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
    bool ValidateToken(string token, out System.Security.Claims.ClaimsPrincipal? principal);
}
