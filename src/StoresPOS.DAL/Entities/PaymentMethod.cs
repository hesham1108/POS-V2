namespace StoresPOS.DAL.Entities;

/// <summary>
/// Seeded method ids (legacy enum kept for compile-time constants and existing tests).
/// Runtime lists come from <see cref="PaymentMethodDefinition"/>; only Kind=Cash enters the drawer.
/// </summary>
public enum PaymentMethod : byte
{
    Cash = 1,
    Card = 2,
    InstaPay = 3,
    VodafoneCash = 4,
    Cheque = 5
}
