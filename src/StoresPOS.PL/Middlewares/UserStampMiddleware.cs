using System.Collections.Concurrent;
using System.Security.Claims;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;

namespace StoresPOS.PL.Middlewares;

/// <summary>
/// Short cache of each user's stamps (Phase 21) so the check below costs one small query per user
/// every <see cref="Ttl"/>. Users screens call <see cref="Invalidate"/> so their own changes apply at once.
/// </summary>
public sealed class UserStampCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<int, (UserStampsDto? Stamps, DateTime ExpiresUtc)> _items = new();

    public async Task<UserStampsDto?> GetAsync(int userId, Func<Task<UserStampsDto?>> load)
    {
        if (_items.TryGetValue(userId, out var hit) && hit.ExpiresUtc > DateTime.UtcNow)
        {
            return hit.Stamps;
        }

        var stamps = await load();
        _items[userId] = (stamps, DateTime.UtcNow.Add(Ttl));
        return stamps;
    }

    public void Invalidate(int userId) => _items.TryRemove(userId, out _);

    public void Clear() => _items.Clear();
}

/// <summary>
/// Runs after authentication (Phase 21):
/// • security stamp changed (signed out everywhere, password reset, deactivated) → the token is dropped;
/// • permission stamp changed (or a token from before Phase 21) → a new token with the current
///   permissions is issued silently, so permission changes apply without signing in again;
/// • "must change password" → every page goes to Account/ChangePassword first.
/// </summary>
public sealed class UserStampMiddleware
{
    private readonly RequestDelegate _next;

    public UserStampMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserStampCache cache,
        IAuthService auth,
        IJwtTokenService tokens)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var stamps = await cache.GetAsync(userId, () => auth.GetStampsAsync(userId, context.RequestAborted));
            var tokenSecurity = user.FindFirstValue(AuthClaims.SecurityStamp);
            var tokenPermission = user.FindFirstValue(AuthClaims.PermissionStamp);

            if (stamps is null
                || !stamps.IsActive
                || (tokenSecurity is not null && tokenSecurity != stamps.SecurityStamp.ToString()))
            {
                SignOut(context);
            }
            else if (tokenPermission != stamps.PermissionStamp.ToString()
                     || tokenSecurity is null
                     || (user.HasClaim(AuthClaims.MustChangePassword, "1") != stamps.MustChangePassword))
            {
                var refreshed = await auth.RefreshAsync(userId, context.RequestAborted);
                if (refreshed.Succeeded && refreshed.AccessToken is not null && refreshed.ExpiresAtUtc is not null
                    && tokens.ValidateToken(refreshed.AccessToken, out var principal) && principal is not null)
                {
                    AppendTokenCookie(context, refreshed.AccessToken, refreshed.ExpiresAtUtc.Value);
                    context.User = principal;
                }
                else
                {
                    SignOut(context);
                }
            }

            if (context.User.HasClaim(AuthClaims.MustChangePassword, "1") && !IsAllowedWhilePasswordChangeDue(context.Request.Path))
            {
                context.Response.Redirect("/Account/ChangePassword");
                return;
            }
        }

        await _next(context);
    }

    private static bool IsAllowedWhilePasswordChangeDue(PathString path) =>
        path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/lib")
        || path.StartsWithSegments("/fonts")
        || path.StartsWithSegments("/uploads");

    private static void SignOut(HttpContext context)
    {
        context.Response.Cookies.Delete(AuthConstants.AccessTokenCookieName);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    public static void AppendTokenCookie(HttpContext context, string token, DateTime expiresAtUtc) =>
        context.Response.Cookies.Append(
            AuthConstants.AccessTokenCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                IsEssential = true
            });
}
