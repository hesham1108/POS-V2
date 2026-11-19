using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class ShiftService : IShiftService
{
    public const decimal MaxCashAmount = 10_000_000m;

    public const string MsgAlreadyOpen = "A shift is already open.";
    public const string MsgNoOpenShift = "There is no open shift.";
    public const string MsgAlreadyClosed = "This shift is already closed.";
    public const string MsgConcurrentClose = "Another user is closing the shift. Refresh and try again.";
    public const string MsgInvalidOpening = "Opening amount must be zero or more.";
    public const string MsgInvalidActual = "Actual cash must be zero or more.";

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public ShiftService(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<ShiftGateDto> GetGateAsync(int userId, CancellationToken cancellationToken = default)
    {
        var open = await GetOpenShiftAsync(cancellationToken);
        if (open is null)
        {
            return new ShiftGateDto { State = ShiftGateState.NoOpenShift };
        }

        return new ShiftGateDto
        {
            State = open.IsOld ? ShiftGateState.OldOpenShift : ShiftGateState.OpenShift,
            Shift = open,
            IsParticipant = open.Participants.Any(x => x.UserId == userId)
        };
    }

    public async Task<ShiftSummaryDto> OpenAsync(
        decimal? openingAmount,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var amount = PricingCalculator.RoundMoney(openingAmount ?? 0m);
        if (amount < 0 || amount > MaxCashAmount)
        {
            throw new BusinessException(MsgInvalidOpening);
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        if (await _db.Shifts.AnyAsync(x => x.Status == ShiftStatus.Open, cancellationToken))
        {
            throw new BusinessException(MsgAlreadyOpen);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var shift = new Shift
        {
            Status = ShiftStatus.Open,
            OpeningAmount = amount,
            OpenedAtUtc = now,
            OpenedByUserId = userId
        };
        shift.Participants.Add(new ShiftParticipant { UserId = userId, JoinedAtUtc = now });
        _db.Shifts.Add(shift);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // UX_Shifts_SingleOpen: someone opened a shift at the same moment.
            throw new BusinessException(MsgAlreadyOpen);
        }

        return (await GetSummaryAsync(shift.Id, cancellationToken))!;
    }

    public async Task<ShiftSummaryDto> ResumeAsync(int userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var shiftId = await _db.Shifts
            .Where(x => x.Status == ShiftStatus.Open)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(MsgNoOpenShift);

        var joined = await _db.ShiftParticipants
            .AnyAsync(x => x.ShiftId == shiftId && x.UserId == userId, cancellationToken);

        if (!joined)
        {
            var participant = new ShiftParticipant
            {
                ShiftId = shiftId,
                UserId = userId,
                JoinedAtUtc = _time.GetUtcNow().UtcDateTime
            };
            _db.ShiftParticipants.Add(participant);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Double submit: the unique (ShiftId, UserId) row already exists — resume is idempotent.
                _db.Entry(participant).State = EntityState.Detached;
            }
        }

        return (await GetSummaryAsync(shiftId, cancellationToken))!;
    }

    public async Task<ShiftSummaryDto?> GetOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var id = await _db.Shifts
            .AsNoTracking()
            .Where(x => x.Status == ShiftStatus.Open)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id.HasValue ? await GetSummaryAsync(id.Value, cancellationToken) : null;
    }

    public async Task<int?> GetActiveShiftIdForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await _db.ShiftParticipants
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Shift.Status == ShiftStatus.Open)
            .Select(x => (int?)x.ShiftId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ShiftSummaryDto?> GetSummaryAsync(int shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .AsNoTracking()
            .Where(x => x.Id == shiftId)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.OpenedAtUtc,
                OpenedBy = x.OpenedByUser.DisplayName,
                x.OpeningAmount,
                x.ClosedAtUtc,
                ClosedBy = x.ClosedByUser != null ? x.ClosedByUser.DisplayName : null,
                x.ExpectedCash,
                x.ActualCash,
                x.CashDifference,
                x.Reconciliation,
                x.CloseNotes
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (shift is null)
        {
            return null;
        }

        var participants = await _db.ShiftParticipants
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId)
            .OrderBy(x => x.JoinedAtUtc)
            .Select(x => new ShiftParticipantDto
            {
                UserId = x.UserId,
                Username = x.User.Username,
                DisplayName = x.User.DisplayName,
                JoinedAtUtc = x.JoinedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totals = await ComputeTotalsAsync(shiftId, cancellationToken);

        return new ShiftSummaryDto
        {
            Id = shift.Id,
            Status = shift.Status,
            IsOld = shift.Status == ShiftStatus.Open && IsBeforeToday(shift.OpenedAtUtc),
            OpenedAtUtc = shift.OpenedAtUtc,
            OpenedByDisplayName = shift.OpenedBy,
            OpeningAmount = shift.OpeningAmount,
            CashSales = totals.CashSales,
            CashRefunds = totals.CashRefunds,
            CashOutsTotal = totals.CashOuts,
            CashOutsCount = totals.CashOutsCount,
            // Closed shifts show the snapshot taken at close; open shifts are live.
            ExpectedCash = shift.ExpectedCash
                ?? ShiftCalculator.ExpectedCash(shift.OpeningAmount, totals.CashSales, totals.CashRefunds, totals.CashOuts),
            CardSales = totals.CardSales,
            InstaPaySales = totals.InstaPaySales,
            VodafoneCashSales = totals.VodafoneCashSales,
            InvoiceCount = totals.InvoiceCount,
            Participants = participants,
            ClosedAtUtc = shift.ClosedAtUtc,
            ClosedByDisplayName = shift.ClosedBy,
            ActualCash = shift.ActualCash,
            CashDifference = shift.CashDifference,
            Reconciliation = shift.Reconciliation,
            CloseNotes = shift.CloseNotes
        };
    }

    public async Task<ShiftSummaryDto> CloseAsync(
        CloseShiftDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        decimal? actual = dto.ActualCash.HasValue ? PricingCalculator.RoundMoney(dto.ActualCash.Value) : null;
        if (actual is < 0 or > MaxCashAmount)
        {
            throw new BusinessException(MsgInvalidActual);
        }

        await EnsureUserExistsAsync(userId, cancellationToken);

        var shift = await _db.Shifts.FirstOrDefaultAsync(x => x.Id == dto.ShiftId, cancellationToken)
            ?? throw new BusinessException(MsgNoOpenShift);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessException(MsgAlreadyClosed);
        }

        // TODO(Phase 7): refuse while another participant has a non-empty Draft sales invoice
        // ("wait until the sale is completed or held" — see docs/page-composition.md §5).

        var totals = await ComputeTotalsAsync(shift.Id, cancellationToken);
        var expected = ShiftCalculator.ExpectedCash(
            shift.OpeningAmount, totals.CashSales, totals.CashRefunds, totals.CashOuts);
        var (result, difference) = ShiftCalculator.Reconcile(expected, actual);

        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = _time.GetUtcNow().UtcDateTime;
        shift.ClosedByUserId = userId;
        shift.ExpectedCash = expected;
        shift.ActualCash = actual;
        shift.CashDifference = difference;
        shift.Reconciliation = result;
        shift.CloseNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException(MsgConcurrentClose);
        }

        return (await GetSummaryAsync(shift.Id, cancellationToken))!;
    }

    /// <summary>A shift opened before today's local store date counts as "old" (rule 11) — warning only.</summary>
    private bool IsBeforeToday(DateTime openedAtUtc)
    {
        var zone = _time.LocalTimeZone;
        var openedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(openedAtUtc, DateTimeKind.Utc), zone);
        var todayLocal = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).Date;
        return openedLocal.Date < todayLocal;
    }

    private sealed record ShiftTotals(
        decimal CashSales,
        decimal CashRefunds,
        decimal CashOuts,
        int CashOutsCount,
        decimal CardSales,
        decimal InstaPaySales,
        decimal VodafoneCashSales,
        int InvoiceCount);

    /// <summary>
    /// Single place where shift money is aggregated. Sales (Phase 7–8), returns and cash-outs (Phase 9)
    /// plug in here; until those tables exist every total is zero, so expected cash = opening amount.
    /// </summary>
    private static Task<ShiftTotals> ComputeTotalsAsync(int shiftId, CancellationToken cancellationToken)
    {
        _ = shiftId;
        _ = cancellationToken;
        return Task.FromResult(new ShiftTotals(0m, 0m, 0m, 0, 0m, 0m, 0m, 0));
    }

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("User not found or inactive.");
        }
    }
}
