namespace StoresPOS.DAL.Entities;

public enum StockMovementType : byte
{
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    Adjustment = 4,
    Transfer = 5
}
