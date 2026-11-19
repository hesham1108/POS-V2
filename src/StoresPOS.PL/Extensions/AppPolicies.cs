namespace StoresPOS.PL.Extensions;

/// <summary>Policies that combine permissions (single-permission policies are named after the permission).</summary>
public static class AppPolicies
{
    /// <summary>Cash-outs list: CashOuts.Create or Reports.View. Without Reports.View only the current shift is shown.</summary>
    public const string CashOutsView = "CashOuts.View";

    /// <summary>Returns list: Returns.Create or Reports.View. Without Reports.View only the current shift is shown.</summary>
    public const string ReturnsView = "Returns.View";
}
