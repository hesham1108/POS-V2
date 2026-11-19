namespace StoresPOS.DAL.Entities;

/// <summary>Incoming (لينا / receivable from customer) or outgoing (علينا / payable to supplier).</summary>
public enum ChequeDirection : byte
{
    In = 1,
    Out = 2
}
