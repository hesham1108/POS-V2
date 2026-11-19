using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class CashOutService : ICashOutService
{
    public const int MaxReasonLength = 300;

    public const string MsgNotInShift = "Open or join the shift before taking cash out.";
    public const string MsgInvalidAmount = "Enter a valid amount greater than zero.";
    public const string MsgReasonRequired = "Enter the reason for this cash-out.";
    public const string MsgReasonTooLong = "The reason is too long (300 characters max).";

    private readonly ApplicationDbContext _db;
    private readonly IShiftService _shifts;
    private readonly TimeProvider _time;

    public CashOutService(ApplicationDbContext db, IShiftService shifts, TimeProvider time)
    {
        _db = db;
        _shifts = shifts;
        _time = time;
    }

    public async Task<CashOutResultDto> CreateAsync(
        CreateCashOutDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var amount = PricingCalculator.RoundMoney(dto.Amount ?? 0m);
        if (amount <= 0 || amount > ShiftService.MaxCashAmount)
        {
            throw new BusinessException(MsgInvalidAmount);
        }

        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new BusinessException(MsgReasonRequired);
        }

        if (reason.Length > MaxReasonLength)
        {
            throw new BusinessException(MsgReasonTooLong);
        }

        var shiftId = await _shifts.GetActiveShiftIdForUserAsync(userId, cancellationToken)
            ?? throw new BusinessException(MsgNotInShift);

        var before = (await _shifts.GetSummaryAsync(shiftId, cancellationToken))!.ExpectedCash;

        var cashOut = new CashOut
        {
            ShiftId = shiftId,
            UserId = userId,
            Amount = amount,
            Reason = reason,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };
        _db.CashOuts.Add(cashOut);
        await _db.SaveChangesAsync(cancellationToken);

        return new CashOutResultDto
        {
            Id = cashOut.Id,
            ShiftId = shiftId,
            Amount = amount,
            ExpectedCash = PricingCalculator.RoundMoney(before - amount),
            ExceededDrawer = amount > before
        };
    }

    public async Task<IReadOnlyList<CashOutListItemDto>> GetListAsync(
        CashOutFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new CashOutFilterDto();
        var query = _db.CashOuts.AsNoTracking();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < filter.ToUtc.Value);
        }

        if (filter.ShiftId.HasValue)
        {
            query = query.Where(x => x.ShiftId == filter.ShiftId.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(x => x.UserId == filter.UserId.Value);
        }

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 2000);
        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new CashOutListItemDto
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ShiftId = x.ShiftId,
                UserId = x.UserId,
                UserDisplayName = x.User.DisplayName,
                Amount = x.Amount,
                Reason = x.Reason
            })
            .ToListAsync(cancellationToken);
    }
}
