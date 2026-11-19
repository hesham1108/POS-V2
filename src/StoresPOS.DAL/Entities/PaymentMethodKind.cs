namespace StoresPOS.DAL.Entities;

/// <summary>
/// Behaviour group for a payment method. Only <see cref="Cash"/> enters the shift drawer (rule 5).
/// </summary>
public enum PaymentMethodKind : byte
{
    Cash = 1,
    Card = 2,
    Wallet = 3,
    BankTransfer = 4,
    Cheque = 5
}
