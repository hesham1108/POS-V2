namespace StoresPOS.DAL.Entities;

/// <summary>Result of the optional cash count at close (business rule 4).</summary>
public enum ShiftReconciliation : byte
{
    /// <summary>No actual cash entered — never treated as "0 counted".</summary>
    NotReconciled = 1,
    Matched = 2,
    Surplus = 3,
    Shortage = 4
}
