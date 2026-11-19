namespace StoresPOS.BLL.DTOs;

public class CreateCashOutDto
{
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}

public class CashOutResultDto
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Expected drawer cash after this cash-out.</summary>
    public decimal ExpectedCash { get; set; }

    /// <summary>True when the amount was more than the drawer was expected to hold (warning only, not blocked).</summary>
    public bool ExceededDrawer { get; set; }
}

public class CashOutFilterDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? ShiftId { get; set; }
    public int? UserId { get; set; }
    public int Take { get; set; } = 500;
}

public class CashOutListItemDto
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
