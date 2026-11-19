namespace StoresPOS.DAL.Entities;

public enum ChequeStatus : byte
{
    Pending = 1,
    Collected = 2,
    Paid = 3,
    Bounced = 4,
    Cancelled = 5
}
