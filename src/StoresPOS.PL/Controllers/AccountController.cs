using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.ViewModels;

namespace StoresPOS.PL.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IOptionsMonitor<StoreSettings> _store;
    private readonly IWebHostEnvironment _env;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly StoresPOS.PL.Middlewares.UserStampCache _stamps;

    public AccountController(
        IAuthService authService,
        IOptionsMonitor<StoreSettings> store,
        IWebHostEnvironment env,
        IStringLocalizer<SharedResource> localizer,
        StoresPOS.PL.Middlewares.UserStampCache stamps)
    {
        _authService = authService;
        _store = store;
        _env = env;
        _l = localizer;
        _stamps = stamps;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // Production only: seed UI language cookie from Settings when the browser has none.
        // Dev / integration tests keep English as the framework default.
        if (_env.IsProduction()
            && !Request.Cookies.ContainsKey(CookieRequestCultureProvider.DefaultCookieName))
        {
            var ui = string.Equals(_store.CurrentValue.DefaultUiLanguage, "en", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : "ar";
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                LocalizationExtensions.MakeCultureCookieValue(ui),
                new CookieOptions
                {
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    SameSite = SameSiteMode.Lax
                });
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.LoginAsync(
            new LoginRequestDto
            {
                Username = model.Username,
                Password = model.Password
            },
            cancellationToken);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AccessToken) || result.ExpiresAtUtc is null)
        {
            var message = _l[result.ErrorMessage ?? "Login failed."].Value;
            ModelState.AddModelError(string.Empty, result.LockedMinutes is { } minutes ? string.Format(message, minutes) : message);
            return View(model);
        }

        AppendAccessTokenCookie(result.AccessToken, result.ExpiresAtUtc.Value);
        if (result.MustChangePassword)
        {
            return RedirectToAction(nameof(ChangePassword), new { returnUrl = model.ReturnUrl });
        }

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthConstants.AccessTokenCookieName);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Change own password (Phase 21). Required first when an admin reset it with "must change".</summary>
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword(string? returnUrl = null)
    {
        ViewData["Title"] = _l["Change password"].Value;
        ViewData["Section"] = _l["Change password"].Value;
        return View(new ChangePasswordViewModel
        {
            ReturnUrl = returnUrl,
            Required = User.HasClaim(AuthClaims.MustChangePassword, "1")
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _l["Change password"].Value;
        ViewData["Section"] = _l["Change password"].Value;
        model.Required = User.HasClaim(AuthClaims.MustChangePassword, "1");
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), _l["The two new passwords do not match."].Value);
            return View(model);
        }

        var userId = this.GetRequiredUserId();
        var result = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword, cancellationToken);
        if (!result.Succeeded || result.AccessToken is null || result.ExpiresAtUtc is null)
        {
            ModelState.AddModelError(string.Empty, _l[result.ErrorMessage ?? "Login failed."].Value);
            return View(model);
        }

        _stamps.Invalidate(userId);
        AppendAccessTokenCookie(result.AccessToken, result.ExpiresAtUtc.Value);
        TempData["Success"] = _l["Password changed."].Value;
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private void AppendAccessTokenCookie(string token, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(
            AuthConstants.AccessTokenCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                IsEssential = true
            });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
