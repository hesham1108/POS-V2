using StoresPOS.PL.Printing;

namespace StoresPOS.PL.Tests;

public class Code128Tests
{
    [Fact]
    public void Patterns_AreTheStandardTable()
    {
        Assert.Equal(107, Code128.Patterns.Count);
        Assert.Equal(107, Code128.Patterns.Distinct().Count());
        for (var i = 0; i < 106; i++)
        {
            Assert.Equal(6, Code128.Patterns[i].Length);
            Assert.Equal(11, Code128.Patterns[i].Sum(c => c - '0'));
        }

        Assert.Equal(13, Code128.Patterns[106].Sum(c => c - '0')); // stop
    }

    [Fact]
    public void Encode_AddsStartChecksumStop()
    {
        // PJJ123C: 104 + 48·1 + 42·2 + 42·3 + 17·4 + 18·5 + 19·6 + 35·7 = 879 → 879 mod 103 = 55
        var values = Code128.Encode("PJJ123C");
        Assert.Equal(104, values[0]);
        Assert.Equal(55, values[^2]);
        Assert.Equal(106, values[^1]);
    }

    [Fact]
    public void ToSvg_RendersInvoiceNumber()
    {
        var svg = Code128.ToSvg("S-000318");
        Assert.StartsWith("<svg", svg);
        Assert.Contains("aria-label=\"S-000318\"", svg);
    }

    [Fact]
    public void Encode_RejectsNonAscii()
    {
        Assert.Throws<ArgumentException>(() => Code128.Encode("فاتورة"));
    }
}
