using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
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

    public AccountController(
        IAuthService authService,
        IOptionsMonitor<StoreSettings> store,
        IWebHostEnvironment env)
    {
        _authService = authService;
        _store = store;
        _env = env;
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
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Login failed.");
            return View(model);
        }

        AppendAccessTokenCookie(result.AccessToken, result.ExpiresAtUtc.Value);
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
