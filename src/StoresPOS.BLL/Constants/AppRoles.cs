namespace StoresPOS.BLL.Constants;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";

    public static readonly string[] All = [Admin, Manager, Cashier];
}
