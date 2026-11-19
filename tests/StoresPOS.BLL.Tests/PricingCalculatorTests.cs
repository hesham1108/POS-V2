using StoresPOS.BLL.Services;

namespace StoresPOS.BLL.Tests;

public class PricingCalculatorTests
{
    [Fact]
    public void CalculateSellPrice_AppliesMarkup()
    {
        var sell = PricingCalculator.CalculateSellPrice(65m, 20m);
        Assert.Equal(78m, sell);
    }

    [Fact]
    public void CalculateMarkupPercent_FromSellPrice()
    {
        var markup = PricingCalculator.CalculateMarkupPercent(65m, 78m);
        Assert.Equal(20m, markup);
    }

    [Fact]
    public void ResolvePricePair_PrefersExplicitSellPrice()
    {
        var (sell, markup) = PricingCalculator.ResolvePricePair(100m, sellPrice: 130m, markupPercent: 99m);
        Assert.Equal(130m, sell);
        Assert.Equal(30m, markup);
    }

    [Fact]
    public void ResolvePricePair_UsesMarkupWhenSellMissing()
    {
        var (sell, markup) = PricingCalculator.ResolvePricePair(50m, sellPrice: null, markupPercent: 10m);
        Assert.Equal(55m, sell);
        Assert.Equal(10m, markup);
    }

    [Fact]
    public void CalculateMarkupPercent_ZeroCost_ReturnsZero()
    {
        Assert.Equal(0m, PricingCalculator.CalculateMarkupPercent(0m, 25m));
    }
}
