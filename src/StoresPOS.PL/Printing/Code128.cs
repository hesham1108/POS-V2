using System.Globalization;
using System.Text;

namespace StoresPOS.PL.Printing;

/// <summary>
/// Offline Code 128 (set B) barcode as inline SVG — no fonts, scripts or network needed.
/// Used for the invoice number on receipts so returns (Phase 9) can scan it.
/// </summary>
public static class Code128
{
    // Bar/space module widths for symbol values 0–106 (106 = stop, 7 elements).
    public static readonly IReadOnlyList<string> Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>Symbol values: start B, data, checksum, stop.</summary>
    public static IReadOnlyList<int> Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = new List<int> { StartB };
        foreach (var ch in text)
        {
            if (ch < 32 || ch > 126)
            {
                throw new ArgumentException($"Character '{ch}' is not in Code 128 set B.", nameof(text));
            }

            values.Add(ch - 32);
        }

        var checksum = StartB;
        for (var i = 1; i < values.Count; i++)
        {
            checksum += values[i] * i;
        }

        values.Add(checksum % 103);
        values.Add(Stop);
        return values;
    }

    /// <summary>SVG barcode. Width scales to its container; bars stay crisp on thermal printers.</summary>
    public static string ToSvg(string text, int height = 48, int quietZoneModules = 10)
    {
        var modules = new List<(int X, int Width)>();
        var x = quietZoneModules;
        foreach (var value in Encode(text))
        {
            var pattern = Patterns[value];
            for (var i = 0; i < pattern.Length; i++)
            {
                var width = pattern[i] - '0';
                if (i % 2 == 0)
                {
                    modules.Add((x, width)); // even positions are bars
                }

                x += width;
            }
        }

        var total = x + quietZoneModules;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {height}\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(text)}\" class=\"barcode\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>");
        foreach (var (bx, bw) in modules)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{bx}\" y=\"0\" width=\"{bw}\" height=\"{height}\" fill=\"#000\"/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
