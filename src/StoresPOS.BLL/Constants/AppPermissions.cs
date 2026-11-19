namespace StoresPOS.BLL.Constants;

/// <summary>
/// Permission keys used for authorization policies and the <c>perm</c> JWT claim.
/// Default grants follow the roles matrix in plan.md; Admin can toggle them per role later (Users &amp; Roles).
/// </summary>
public static class AppPermissions
{
    public const string ClaimType = "perm";

    public const string PosUse = "Pos.Use";
    public const string ShiftsOpenResume = "Shifts.OpenResume";
    public const string ShiftsClose = "Shifts.Close";
    public const string SalesPriceOverride = "Sales.PriceOverride";
    public const string SalesDiscount = "Sales.Discount";
    public const string CashOutsCreate = "CashOuts.Create";
    public const string ReturnsCreate = "Returns.Create";
    public const string CatalogManage = "Catalog.Manage";
    public const string PurchasesManage = "Purchases.Manage";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string DashboardView = "Dashboard.View";
    public const string ReportsView = "Reports.View";
    public const string UsersManage = "Users.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string PaymentMethodsManage = "PaymentMethods.Manage";
    public const string TreasuriesView = "Treasuries.View";
    public const string TreasuriesMove = "Treasuries.Move";
    public const string TreasuriesManage = "Treasuries.Manage";
    public const string ChequesView = "Cheques.View";
    public const string ChequesManage = "Cheques.Manage";
    public const string ChequesChangeStatus = "Cheques.ChangeStatus";

    public static readonly string[] All =
    [
        PosUse, ShiftsOpenResume, ShiftsClose, SalesPriceOverride, SalesDiscount,
        CashOutsCreate, ReturnsCreate, CatalogManage, PurchasesManage, InventoryAdjust,
        DashboardView, ReportsView, UsersManage, SettingsManage, PaymentMethodsManage,
        TreasuriesView, TreasuriesMove, TreasuriesManage,
        ChequesView, ChequesManage, ChequesChangeStatus
    ];

    private static readonly string[] ManagerDefaults =
    [
        PosUse, ShiftsOpenResume, ShiftsClose, SalesPriceOverride, SalesDiscount,
        CashOutsCreate, ReturnsCreate, CatalogManage, PurchasesManage, InventoryAdjust,
        DashboardView, ReportsView, TreasuriesView, TreasuriesMove,
        ChequesView, ChequesManage, ChequesChangeStatus
        // PaymentMethods.Manage / Treasuries.Manage are Optional for Manager — start OFF
    ];

    /// <summary>Cashier "Optional" cells (close, price, discount, returns) start OFF.</summary>
    private static readonly string[] CashierDefaults =
    [
        PosUse, ShiftsOpenResume, CashOutsCreate
    ];

    public static IReadOnlyList<string> DefaultsFor(string roleName) => roleName switch
    {
        AppRoles.Admin => All,
        AppRoles.Manager => ManagerDefaults,
        AppRoles.Cashier => CashierDefaults,
        _ => []
    };
}
