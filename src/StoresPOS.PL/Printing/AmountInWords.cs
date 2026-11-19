using System.Globalization;

namespace StoresPOS.PL.Printing;

/// <summary>EGP amount in words for A4 invoices (Arabic + English).</summary>
public static class AmountInWords
{
    private static readonly string[] EnOnes =
    [
        "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen"
    ];

    private static readonly string[] EnTens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static readonly string[] ArOnes =
    [
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
        "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر",
        "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    ];

    private static readonly string[] ArTens =
        ["", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];

    private static readonly string[] ArHundreds =
        ["", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"];

    public static string Format(decimal amount, string language)
    {
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var pounds = (long)Math.Truncate(amount);
        var piastres = (int)Math.Round((amount - pounds) * 100m, MidpointRounding.AwayFromZero);
        if (piastres == 100)
        {
            pounds++;
            piastres = 0;
        }

        return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? FormatAr(pounds, piastres)
            : FormatEn(pounds, piastres);
    }

    private static string FormatEn(long pounds, int piastres)
    {
        var main = pounds == 0 ? "zero" : UnderThousandEn(pounds);
        var result = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(main) + " Egyptian pounds";
        if (piastres > 0)
        {
            result += " and " + UnderThousandEn(piastres) + " piastres";
        }

        return result + " only";
    }

    private static string FormatAr(long pounds, int piastres)
    {
        var main = pounds == 0 ? "صفر" : UnderThousandAr(pounds);
        var result = main + " جنيه مصري";
        if (piastres > 0)
        {
            result += " و" + UnderThousandAr(piastres) + " قرش";
        }

        return result + " فقط لا غير";
    }

    private static string UnderThousandEn(long n)
    {
        if (n < 0) return UnderThousandEn(-n);
        if (n < 20) return EnOnes[n];
        if (n < 100) return EnTens[n / 10] + (n % 10 == 0 ? "" : "-" + EnOnes[n % 10]);
        if (n < 1000)
        {
            var rest = n % 100;
            return EnOnes[n / 100] + " hundred" + (rest == 0 ? "" : " " + UnderThousandEn(rest));
        }

        if (n < 1_000_000)
        {
            var rest = n % 1000;
            return UnderThousandEn(n / 1000) + " thousand" + (rest == 0 ? "" : " " + UnderThousandEn(rest));
        }

        var millions = n / 1_000_000;
        var rem = n % 1_000_000;
        return UnderThousandEn(millions) + " million" + (rem == 0 ? "" : " " + UnderThousandEn(rem));
    }

    private static string UnderThousandAr(long n)
    {
        if (n < 0) return UnderThousandAr(-n);
        if (n < 20) return ArOnes[n];
        if (n < 100)
        {
            var ones = n % 10;
            var tens = n / 10;
            return ones == 0 ? ArTens[tens] : ArOnes[ones] + " و" + ArTens[tens];
        }

        if (n < 1000)
        {
            var rest = n % 100;
            return ArHundreds[n / 100] + (rest == 0 ? "" : " و" + UnderThousandAr(rest));
        }

        if (n < 1_000_000)
        {
            var thousands = n / 1000;
            var rest = n % 1000;
            var head = thousands switch
            {
                1 => "ألف",
                2 => "ألفان",
                >= 3 and <= 10 => UnderThousandAr(thousands) + " آلاف",
                _ => UnderThousandAr(thousands) + " ألف"
            };
            return head + (rest == 0 ? "" : " و" + UnderThousandAr(rest));
        }

        var millions = n / 1_000_000;
        var rem = n % 1_000_000;
        var mHead = millions switch
        {
            1 => "مليون",
            2 => "مليونان",
            _ => UnderThousandAr(millions) + " مليون"
        };
        return mHead + (rem == 0 ? "" : " و" + UnderThousandAr(rem));
    }
}
