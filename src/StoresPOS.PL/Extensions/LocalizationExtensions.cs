using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Localization;

namespace StoresPOS.PL.Extensions;

public static class LocalizationExtensions
{
    /// <summary>
    /// UI language (strings + RTL) switches between en/ar, while number/date formatting and
    /// model binding stay en-US: Western digits on screen (open decision) and "500.50" always parses.
    /// </summary>
    public const string FormattingCulture = "en-US";
    public static readonly string[] UiCultures = ["en", "ar"];

    public static IServiceCollection AddStoresPosLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Emit Arabic as real characters instead of &#x...; entities (smaller, readable HTML).
        services.Configure<WebEncoderOptions>(options =>
            options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(FormattingCulture, "en");
            options.SupportedCultures = [new CultureInfo(FormattingCulture)];
            options.SupportedUICultures = UiCultures.Select(c => new CultureInfo(c)).ToList();
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider()
            ];
        });

        return services;
    }

    public static string MakeCultureCookieValue(string uiCulture) =>
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(FormattingCulture, uiCulture));
}
