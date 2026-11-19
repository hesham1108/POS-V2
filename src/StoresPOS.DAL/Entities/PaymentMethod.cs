namespace StoresPOS.DAL.Entities;

/// <summary>Only <see cref="Cash"/> enters the drawer (business rule 5).</summary>
public enum PaymentMethod : byte
{
    Cash = 1,
    Card = 2,
    InstaPay = 3,
    VodafoneCash = 4
}
