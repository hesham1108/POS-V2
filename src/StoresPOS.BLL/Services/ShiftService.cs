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
    public const string MsgSaleInProgress = "{0} has an invoice in progress. Wait until it is paid or held.";
    public const string MsgHandoverTreasury = "Choose a treasury for the drawer handover.";

    private readonly ApplicationDbContext _db;
    private readonly ITreasuryService _treasuries;
    private readonly TimeProvider _time;

    public ShiftService(ApplicationDbContext db, ITreasuryService treasuries, TimeProvider time)
    {
        _db = db;
        _treasuries = treasuries;
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
                x.CloseNotes,
                x.HandoverTreasuryId,
                x.HandoverAmount,
                HandoverNameEn = x.HandoverTreasury != null ? x.HandoverTreasury.NameEn : null,
                HandoverNameAr = x.HandoverTreasury != null ? x.HandoverTreasury.NameAr : null
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
        foreach (var participant in participants)
        {
            if (totals.ByCashier.TryGetValue(participant.UserId, out var mine))
            {
                participant.InvoiceCount = mine.InvoiceCount;
                participant.CashSales = mine.CashSales;
                participant.NonCashSales = mine.NonCashSales;
            }

            participant.CashOuts = totals.CashOutsByUser.GetValueOrDefault(participant.UserId);
        }

        var heldCount = await _db.SalesInvoices
            .CountAsync(x => x.ShiftId == shiftId && x.Status == SalesInvoiceStatus.Held, cancellationToken);
        var inProgress = await GetInProgressSalesAsync(shiftId, cancellationToken);

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
            NonCashRefunds = totals.NonCashRefunds,
            ReturnsCount = totals.ReturnsCount,
            // Closed shifts show the snapshot taken at close; open shifts are live.
            ExpectedCash = shift.ExpectedCash
                ?? ShiftCalculator.ExpectedCash(shift.OpeningAmount, totals.CashSales, totals.CashRefunds, totals.CashOuts),
            CardSales = totals.CardSales,
            InstaPaySales = totals.InstaPaySales,
            VodafoneCashSales = totals.VodafoneCashSales,
            InvoiceCount = totals.InvoiceCount,
            Participants = participants,
            HeldInvoiceCount = heldCount,
            InProgressSales = inProgress,
            ClosedAtUtc = shift.ClosedAtUtc,
            ClosedByDisplayName = shift.ClosedBy,
            ActualCash = shift.ActualCash,
            CashDifference = shift.CashDifference,
            Reconciliation = shift.Reconciliation,
            CloseNotes = shift.CloseNotes,
            HandoverTreasuryId = shift.HandoverTreasuryId,
            HandoverAmount = shift.HandoverAmount,
            HandoverTreasuryNameEn = shift.HandoverNameEn,
            HandoverTreasuryNameAr = shift.HandoverNameAr
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

        // A cashier mid-sale: wait until that invoice is paid or held (docs/page-composition.md §5).
        var inProgress = await GetInProgressSalesAsync(shift.Id, cancellationToken);
        if (inProgress.Count > 0)
        {
            throw new BusinessException(MsgSaleInProgress, inProgress[0].CashierDisplayName);
        }

        // Held invoices and empty carts do not survive the shift (plan.md open decisions).
        var leftovers = await _db.SalesInvoices
            .Where(x => x.ShiftId == shift.Id
                        && (x.Status == SalesInvoiceStatus.Held || x.Status == SalesInvoiceStatus.Draft))
            .ToListAsync(cancellationToken);
        foreach (var invoice in leftovers)
        {
            invoice.Status = SalesInvoiceStatus.Cancelled;
            invoice.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        }

        var totals = await ComputeTotalsAsync(shift.Id, cancellationToken);
        var expected = ShiftCalculator.ExpectedCash(
            shift.OpeningAmount, totals.CashSales, totals.CashRefunds, totals.CashOuts);
        var (result, difference) = ShiftCalculator.Reconcile(expected, actual);

        // Counted cash when reconciled; otherwise expected (journey L / Phase 12).
        var handoverAmount = PricingCalculator.RoundMoney(actual ?? expected);
        if (dto.HandoverTreasuryId <= 0)
        {
            throw new BusinessException(MsgHandoverTreasury);
        }

        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = _time.GetUtcNow().UtcDateTime;
        shift.ClosedByUserId = userId;
        shift.ExpectedCash = expected;
        shift.ActualCash = actual;
        shift.CashDifference = difference;
        shift.Reconciliation = result;
        shift.CloseNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        shift.HandoverTreasuryId = dto.HandoverTreasuryId;
        shift.HandoverAmount = handoverAmount;

        try
        {
            await _treasuries.RecordShiftHandoverAsync(
                dto.HandoverTreasuryId, handoverAmount, shift.Id, userId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException(MsgConcurrentClose);
        }

        return (await GetSummaryAsync(shift.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<ShiftHistoryItemDto>> GetHistoryAsync(
        ShiftHistoryFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ShiftHistoryFilterDto();
        var query = _db.Shifts.AsNoTracking().AsQueryable();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.OpenedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.OpenedAtUtc < filter.ToUtc.Value);
        }

        if (filter.UserId.HasValue)
        {
            var uid = filter.UserId.Value;
            query = query.Where(x => x.OpenedByUserId == uid
                                     || x.ClosedByUserId == uid
                                     || x.Participants.Any(p => p.UserId == uid));
        }

        query = filter.Result switch
        {
            ShiftResultFilter.Open => query.Where(x => x.Status == ShiftStatus.Open),
            ShiftResultFilter.Matched => query.Where(x => x.Reconciliation == ShiftReconciliation.Matched),
            ShiftResultFilter.Surplus => query.Where(x => x.Reconciliation == ShiftReconciliation.Surplus),
            ShiftResultFilter.Shortage => query.Where(x => x.Reconciliation == ShiftReconciliation.Shortage),
            ShiftResultFilter.NotReconciled => query.Where(x => x.Reconciliation == ShiftReconciliation.NotReconciled),
            _ => query
        };

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 2000);
        var rows = await query
            .OrderByDescending(x => x.OpenedAtUtc)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.OpenedAtUtc,
                x.ClosedAtUtc,
                OpenedBy = x.OpenedByUser.DisplayName,
                ClosedBy = x.ClosedByUser != null ? x.ClosedByUser.DisplayName : null,
                Participants = x.Participants.OrderBy(p => p.JoinedAtUtc).Select(p => p.User.DisplayName).ToList(),
                x.ExpectedCash,
                x.ActualCash,
                x.CashDifference,
                x.Reconciliation
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(x => x.Id).ToList();
        var sales = (await _db.SalesInvoices.AsNoTracking()
                .Where(x => ids.Contains(x.ShiftId) && x.Status == SalesInvoiceStatus.Completed)
                .Select(x => new { x.ShiftId, x.Total })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ShiftId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(x => x.Total)));

        var result = new List<ShiftHistoryItemDto>(rows.Count);
        foreach (var row in rows)
        {
            var expected = row.ExpectedCash;
            if (row.Status == ShiftStatus.Open)
            {
                // Not snapshotted yet: use the live drawer figure.
                expected = (await GetSummaryAsync(row.Id, cancellationToken))?.ExpectedCash ?? 0m;
            }

            var s = sales.GetValueOrDefault(row.Id);
            result.Add(new ShiftHistoryItemDto
            {
                Id = row.Id,
                Status = row.Status,
                OpenedAtUtc = row.OpenedAtUtc,
                ClosedAtUtc = row.ClosedAtUtc,
                OpenedByDisplayName = row.OpenedBy,
                ClosedByDisplayName = row.ClosedBy,
                Participants = row.Participants,
                InvoiceCount = s.Count,
                SalesTotal = s.Total,
                ExpectedCash = expected ?? 0m,
                ActualCash = row.ActualCash,
                CashDifference = row.CashDifference,
                Reconciliation = row.Reconciliation
            });
        }

        return result;
    }

    /// <summary>A shift opened before today's local store date counts as "old" (rule 11) — warning only.</summary>
    private bool IsBeforeToday(DateTime openedAtUtc)
    {
        var zone = _time.LocalTimeZone;
        var openedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(openedAtUtc, DateTimeKind.Utc), zone);
        var todayLocal = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).Date;
        return openedLocal.Date < todayLocal;
    }

    private sealed record CashierTotals(int InvoiceCount, decimal CashSales, decimal NonCashSales);

    private sealed record ShiftTotals(
        decimal CashSales,
        decimal CashRefunds,
        decimal NonCashRefunds,
        int ReturnsCount,
        decimal CashOuts,
        int CashOutsCount,
        IReadOnlyDictionary<int, decimal> CashOutsByUser,
        decimal CardSales,
        decimal InstaPaySales,
        decimal VodafoneCashSales,
        int InvoiceCount,
        IReadOnlyDictionary<int, CashierTotals> ByCashier);

    /// <summary>
    /// Single place where shift money is aggregated: completed sales, returns refunded in this shift
    /// (cash leaves the drawer; non-cash is recorded only) and cash-outs.
    /// </summary>
    private async Task<ShiftTotals> ComputeTotalsAsync(int shiftId, CancellationToken cancellationToken)
    {
        var sales = await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId && x.Status == SalesInvoiceStatus.Completed)
            .Select(x => new { x.CashierUserId, x.PaymentMethod, x.Total })
            .ToListAsync(cancellationToken);

        var refunds = await _db.SalesReturns
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId)
            .Select(x => new { x.RefundMethod, x.TotalAmount })
            .ToListAsync(cancellationToken);

        var cashOuts = await _db.CashOuts
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId)
            .Select(x => new { x.UserId, x.Amount })
            .ToListAsync(cancellationToken);

        // Kind=Cash methods enter the drawer; seed Cash id=1, plus any custom cash methods.
        var cashIds = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.Kind == PaymentMethodKind.Cash)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (cashIds.Count == 0)
        {
            cashIds = [(byte)PaymentMethod.Cash];
        }

        bool IsCashSale(PaymentMethod? m) => m.HasValue && cashIds.Contains((byte)m.Value);
        bool IsCashRefund(PaymentMethod m) => cashIds.Contains((byte)m);

        decimal Sum(PaymentMethod method) => sales.Where(x => x.PaymentMethod == method).Sum(x => x.Total);

        var byCashier = sales
            .GroupBy(x => x.CashierUserId)
            .ToDictionary(
                g => g.Key,
                g => new CashierTotals(
                    g.Count(),
                    g.Where(x => IsCashSale(x.PaymentMethod)).Sum(x => x.Total),
                    g.Where(x => !IsCashSale(x.PaymentMethod)).Sum(x => x.Total)));

        return new ShiftTotals(
            CashSales: sales.Where(x => IsCashSale(x.PaymentMethod)).Sum(x => x.Total),
            CashRefunds: refunds.Where(x => IsCashRefund(x.RefundMethod)).Sum(x => x.TotalAmount),
            NonCashRefunds: refunds.Where(x => !IsCashRefund(x.RefundMethod)).Sum(x => x.TotalAmount),
            ReturnsCount: refunds.Count,
            CashOuts: cashOuts.Sum(x => x.Amount),
            CashOutsCount: cashOuts.Count,
            CashOutsByUser: cashOuts.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount)),
            CardSales: Sum(PaymentMethod.Card),
            InstaPaySales: Sum(PaymentMethod.InstaPay),
            VodafoneCashSales: Sum(PaymentMethod.VodafoneCash),
            InvoiceCount: sales.Count,
            ByCashier: byCashier);
    }

    private async Task<IReadOnlyList<InProgressSaleDto>> GetInProgressSalesAsync(
        int shiftId,
        CancellationToken cancellationToken) =>
        await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId && x.Status == SalesInvoiceStatus.Draft && x.Items.Any())
            .OrderBy(x => x.UpdatedAtUtc)
            .Select(x => new InProgressSaleDto
            {
                InvoiceId = x.Id,
                CashierUserId = x.CashierUserId,
                CashierDisplayName = x.CashierUser.DisplayName,
                LineCount = x.Items.Count,
                Total = x.Total
            })
            .ToListAsync(cancellationToken);

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new BusinessException("User not found or inactive.");
        }
    }
}
