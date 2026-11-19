namespace StoresPOS.DAL.Entities;

public enum TreasuryTransactionSourceType : byte
{
    Manual = 1,
    Transfer = 2,
    ShiftClose = 3,
    Sale = 4,
    Purchase = 5,
    Cheque = 6
}
