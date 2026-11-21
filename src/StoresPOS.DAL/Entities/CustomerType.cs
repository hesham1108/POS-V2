namespace StoresPOS.DAL.Entities;

/// <summary>
/// What kind of customer this is (classification for lists and reports).
/// Separate from <see cref="Customer.DefaultSaleType"/>, which only decides the price level.
/// </summary>
public enum CustomerType : byte
{
    Retail = 1,
    Wholesale = 2,
    Company = 3
}