using System.Globalization;
using StoresPOS.BLL.Models;

namespace StoresPOS.PL.Printing;

/// <summary>
/// Formatting for printed documents: store language (not the cashier's UI language) and the
/// receipt-digits setting. Call <see cref="ApplyCulture"/> at the top of each print view/layout.
/// </summary>
public sealed class ReceiptFormat
{
    private static readonly char[] ArabicIndic = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

    public ReceiptFormat(StoreSettings settings)
    {
        Settings = settings;
        Language = settings.ReceiptLanguage == "en" ? "en" : "ar";
    }

    public StoreSettings Settings { get; }
    public string Language { get; }
    public bool IsRtl => Language == "ar";
    public bool ArabicDigits => Settings.ReceiptDigits == "arabic";

    public void ApplyCulture() => CultureInfo.CurrentUICulture = new CultureInfo(Language);

    /// <summary>Receipt language for labels until disposed, then the previous UI culture again (Phase 24).</summary>
    public IDisposable UseCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        ApplyCulture();
        return new Restore(previous);
    }

    private sealed class Restore(CultureInfo previous) : IDisposable
    {
        public void Dispose() => CultureInfo.CurrentUICulture = previous;
    }

    public string Money(decimal value) => Digits(value.ToString("N2", CultureInfo.InvariantCulture));
    public string Qty(decimal value) => Digits(value.ToString("0.###", CultureInfo.InvariantCulture));
    public string DateTime(DateTime utc) => Digits(utc.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));

    public string Digits(string text)
    {
        if (!ArabicDigits)
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = ArabicIndic[chars[i] - '0'];
            }
            else if (chars[i] == '.')
            {
                chars[i] = '٫';
            }
            else if (chars[i] == ',')
            {
                chars[i] = '٬';
            }
        }

        return new string(chars);
    }
}
