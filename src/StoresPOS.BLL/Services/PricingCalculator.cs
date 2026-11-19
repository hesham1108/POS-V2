namespace StoresPOS.BLL.Services;

/// <summary>
/// Markup helpers: sell = cost × (1 + markup/100); markup = ((sell − cost) / cost) × 100.
/// Retail and wholesale are independent.
/// </summary>
public static class PricingCalculator
{
    public static decimal CalculateSellPrice(decimal cost, decimal markupPercent)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }

        return RoundMoney(cost * (1 + (markupPercent / 100m)));
    }

    public static decimal CalculateMarkupPercent(decimal cost, decimal sellPrice)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }

        if (cost == 0)
        {
            return 0m;
        }

        return RoundMarkup(((sellPrice - cost) / cost) * 100m);
    }

    /// <summary>
    /// Resolves sell price and markup from optional inputs.
    /// Prefer explicit sell price when provided; otherwise derive from markup.
    /// </summary>
    public static (decimal SellPrice, decimal MarkupPercent) ResolvePricePair(
        decimal cost,
        decimal? sellPrice,
        decimal? markupPercent)
    {
        if (sellPrice.HasValue)
        {
            if (sellPrice.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sellPrice), "Sell price cannot be negative.");
            }

            var price = RoundMoney(sellPrice.Value);
            return (price, CalculateMarkupPercent(cost, price));
        }

        var markup = RoundMarkup(markupPercent ?? 0m);
        return (CalculateSellPrice(cost, markup), markup);
    }

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundMarkup(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
