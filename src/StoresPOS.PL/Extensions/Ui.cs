using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace StoresPOS.PL;

/// <summary>
/// Display names in the UI language (Phase 25, plan §6 task 4): entities with an Arabic and an English name
/// show the Arabic one in the Arabic UI and the English one otherwise, falling back to whichever exists.
/// Printed documents use the receipt language instead (see Printing/ReceiptFormat).
/// </summary>
public static class Ui
{
    public static bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public static string Name(string? en, string? ar)
    {
        var first = IsArabic ? ar : en;
        var second = IsArabic ? en : ar;
        return !string.IsNullOrWhiteSpace(first) ? first! : second ?? string.Empty;
    }

    public static string? NameOrNull(string? en, string? ar) =>
        string.IsNullOrWhiteSpace(en) && string.IsNullOrWhiteSpace(ar) ? null : Name(en, ar);

    /// <summary>Dropdown items for an enum with translated names (key = member name, or the given label key).</summary>
    public static IEnumerable<SelectListItem> EnumItems<T>(IStringLocalizer l, Func<T, string>? labelKey = null)
        where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new SelectListItem(
            l[labelKey?.Invoke(v) ?? v.ToString()].Value,
            Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
}
