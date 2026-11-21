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
    public const string WarehousesView = "Warehouses.View";
    public const string WarehousesTransfer = "Warehouses.Transfer";
    public const string WarehousesManage = "Warehouses.Manage";

    /// <summary>Profit, cost and margin in reports (Phase 23, plan §9: Owner/Admin by default).</summary>
    public const string ProfitView = "Profit.View";

    public static readonly string[] All =
    [
        PosUse, ShiftsOpenResume, ShiftsClose, SalesPriceOverride, SalesDiscount,
        CashOutsCreate, ReturnsCreate, CatalogManage, PurchasesManage, InventoryAdjust,
        DashboardView, ReportsView, UsersManage, SettingsManage, PaymentMethodsManage,
        TreasuriesView, TreasuriesMove, TreasuriesManage,
        ChequesView, ChequesManage, ChequesChangeStatus,
        WarehousesView, WarehousesTransfer, WarehousesManage,
        ProfitView
    ];

    private static readonly string[] ManagerDefaults =
    [
        PosUse, ShiftsOpenResume, ShiftsClose, SalesPriceOverride, SalesDiscount,
        CashOutsCreate, ReturnsCreate, CatalogManage, PurchasesManage, InventoryAdjust,
        DashboardView, ReportsView, TreasuriesView, TreasuriesMove,
        ChequesView, ChequesManage, ChequesChangeStatus,
        WarehousesView, WarehousesTransfer
        // PaymentMethods.Manage / Treasuries.Manage / Warehouses.Manage are Optional for Manager — start OFF
    ];

    /// <summary>Cashier "Optional" cells (close, price, discount, returns) start OFF.</summary>
    private static readonly string[] CashierDefaults =
    [
        PosUse, ShiftsOpenResume, CashOutsCreate
    ];

    public static IReadOnlyList<string> DefaultsFor(string roleName) => roleName switch
    {
        AppRoles.Owner => All,
        AppRoles.Admin => All,
        AppRoles.Manager => ManagerDefaults,
        AppRoles.Cashier => CashierDefaults,
        _ => []
    };

    /// <summary>Sidebar-facing permission keys shown as “tabs” in Users UI.</summary>
    public static readonly (string Permission, string LabelEn, string LabelAr)[] NavTabs =
    [
        (DashboardView, "Dashboard", "لوحة التحكم"),
        (PosUse, "POS", "نقطة البيع"),
        (ShiftsOpenResume, "Shifts", "الورديات"),
        (ReportsView, "Sales / Reports / Customers", "المبيعات / التقارير / العملاء"),
        (ReturnsCreate, "Returns", "المرتجعات"),
        (CashOutsCreate, "Cash-outs", "الصرف"),
        (CatalogManage, "Products", "المنتجات"),
        (PurchasesManage, "Purchases / Suppliers", "المشتريات / الموردون"),
        (TreasuriesView, "Treasuries", "الخزائن"),
        (ChequesView, "Cheques", "الشيكات"),
        (WarehousesView, "Warehouses", "المخازن"),
        (PaymentMethodsManage, "Payment methods", "طرق الدفع"),
        (UsersManage, "Users and roles", "المستخدمون والصلاحيات"),
        (SettingsManage, "Settings", "الإعدادات"),
        (SalesPriceOverride, "Price override", "تعديل السعر"),
        (SalesDiscount, "Discount", "الخصم"),
        (ShiftsClose, "Close shift", "إغلاق الوردية"),
        (InventoryAdjust, "Inventory adjust", "تعديل المخزون"),
        (TreasuriesMove, "Treasury moves", "تحويلات الخزينة"),
        (TreasuriesManage, "Manage treasuries", "إدارة الخزائن"),
        (ChequesManage, "Manage cheques", "إدارة الشيكات"),
        (ChequesChangeStatus, "Cheque status", "حالة الشيك"),
        (WarehousesTransfer, "Warehouse transfer", "تحويل مخزني"),
        (WarehousesManage, "Manage warehouses", "إدارة المخازن"),
        (ProfitView, "Profit and cost", "الأرباح والتكلفة")
    ];

    /// <summary>
    /// Permissions grouped for the Users screens (Phase 21). Every key in <see cref="All"/> is in exactly one group.
    /// </summary>
    public static readonly (string Key, string LabelEn, string LabelAr, string[] Permissions)[] Groups =
    [
        ("sales", "Sales", "المبيعات",
            [PosUse, ShiftsOpenResume, ShiftsClose, SalesPriceOverride, SalesDiscount, ReturnsCreate, CashOutsCreate]),
        ("inventory", "Inventory", "المخزون",
            [CatalogManage, PurchasesManage, InventoryAdjust, WarehousesView, WarehousesTransfer, WarehousesManage]),
        ("finance", "Finance", "المالية",
            [TreasuriesView, TreasuriesMove, TreasuriesManage, ChequesView, ChequesManage, ChequesChangeStatus, PaymentMethodsManage]),
        ("reports", "Reports", "التقارير",
            [DashboardView, ReportsView, ProfitView]),
        ("system", "System", "النظام",
            [UsersManage, SettingsManage])
    ];

    /// <summary>Ready-made roles a user can copy and adjust (Phase 21).</summary>
    public static readonly (string Key, string NameEn, string NameAr, string[] Permissions)[] RoleTemplates =
    [
        ("cashier", "Cashier", "كاشير", CashierDefaults),
        ("manager", "Manager", "مدير", ManagerDefaults),
        ("accountant", "Accountant", "محاسب",
            [DashboardView, ReportsView, TreasuriesView, TreasuriesMove, ChequesView, ChequesManage, ChequesChangeStatus]),
        ("storekeeper", "Storekeeper", "أمين مخزن",
            [CatalogManage, PurchasesManage, InventoryAdjust, WarehousesView, WarehousesTransfer])
    ];

    public static (string LabelEn, string LabelAr) LabelFor(string permission)
    {
        foreach (var tab in NavTabs)
        {
            if (tab.Permission == permission)
            {
                return (tab.LabelEn, tab.LabelAr);
            }
        }

        return (permission, permission);
    }
}
