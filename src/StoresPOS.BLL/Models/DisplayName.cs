using System.Globalization;

namespace StoresPOS.BLL.Models;

/// <summary>
/// An entity name used as a message argument (Phase 25): shows the Arabic name in the Arabic UI and the English
/// one otherwise, when the web layer formats the translated message.
/// </summary>
public sealed record DisplayName(string En, string Ar)
{
    public override string ToString() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" && !string.IsNullOrWhiteSpace(Ar) ? Ar : En;
}
