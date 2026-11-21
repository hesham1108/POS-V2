namespace StoresPOS.BLL.Constants;

public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";

    public static readonly string[] All = [Owner, Admin, Manager, Cashier];

    public static bool IsOwner(string? roleName) =>
        string.Equals(roleName, Owner, StringComparison.OrdinalIgnoreCase);

    public static bool IsOwnerOrAdmin(string? roleName) =>
        IsOwner(roleName) || string.Equals(roleName, Admin, StringComparison.OrdinalIgnoreCase);
}
