namespace StoresPOS.DAL.Entities;

public enum SalesInvoiceStatus : byte
{
    /// <summary>The cashier's live cart (one per cashier). Saved on every change.</summary>
    Draft = 1,

    /// <summary>Parked with F8; resumable with F7 until the shift closes.</summary>
    Held = 2,

    /// <summary>Paid and saved; stock deducted.</summary>
    Completed = 3,

    /// <summary>Held invoice dropped at shift close, or an abandoned draft.</summary>
    Cancelled = 4
}
