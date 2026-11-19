using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.DTOs;

public class TreasuryListItemDto
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public TreasuryType Type { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int MovementCount { get; set; }
}

public class TreasuryDetailDto : TreasuryListItemDto
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class TreasuryFormDto
{
    public int? Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public TreasuryType Type { get; set; } = TreasuryType.Cash;
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class TreasuryMoveDto
{
    public int TreasuryId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class TreasuryTransferDto
{
    public int FromTreasuryId { get; set; }
    public int ToTreasuryId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class TreasuryStatementFilterDto
{
    public int TreasuryId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Take { get; set; } = 500;
}

public class TreasuryTransactionDto
{
    public long Id { get; set; }
    public int TreasuryId { get; set; }
    public TreasuryTransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public TreasuryTransactionSourceType SourceType { get; set; }
    public int? SourceId { get; set; }
    public int UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Note { get; set; }
    public Guid? TransferGroupId { get; set; }

    /// <summary>Running balance after this row (opening + earlier ledger), when statement is ordered ascending.</summary>
    public decimal? RunningBalance { get; set; }
}

public class TreasuryStatementDto
{
    public TreasuryDetailDto Treasury { get; set; } = new();
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public decimal OpeningBalanceAtFrom { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public IReadOnlyList<TreasuryTransactionDto> Rows { get; set; } = [];
}
