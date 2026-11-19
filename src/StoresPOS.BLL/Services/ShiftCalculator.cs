using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public static class ShiftCalculator
{
    /// <summary>Expected drawer = opening + cash sales − cash refunds − cash-outs (cash only, rule 5).</summary>
    public static decimal ExpectedCash(decimal opening, decimal cashSales, decimal cashRefunds, decimal cashOuts) =>
        PricingCalculator.RoundMoney(opening + cashSales - cashRefunds - cashOuts);

    /// <summary>
    /// Rule 4: no count → Not Reconciled (difference null), never "0 counted".
    /// </summary>
    public static (ShiftReconciliation Result, decimal? Difference) Reconcile(decimal expected, decimal? actual)
    {
        if (!actual.HasValue)
        {
            return (ShiftReconciliation.NotReconciled, null);
        }

        var diff = PricingCalculator.RoundMoney(actual.Value - expected);
        var result = diff switch
        {
            0m => ShiftReconciliation.Matched,
            > 0m => ShiftReconciliation.Surplus,
            _ => ShiftReconciliation.Shortage
        };

        return (result, diff);
    }
}
