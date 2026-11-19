using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class TreasuryService : ITreasuryService
{
    public const decimal MaxAmount = 10_000_000m;

    public const string MsgNotFound = "Treasury not found.";
    public const string MsgNameRequired = "English and Arabic names are required.";
    public const string MsgCannotDelete = "Treasuries with movements cannot be deleted. Deactivate instead.";
    public const string MsgInactive = "That treasury is not active.";
    public const string MsgAmount = "Amount must be greater than zero.";
    public const string MsgReasonRequired = "A reason is required.";
    public const string MsgSameTreasury = "Choose two different treasuries for a transfer.";
    public const string MsgInsufficient = "Insufficient balance in {0}.";

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public TreasuryService(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Treasuries.AnyAsync(cancellationToken))
        {
            return;
        }

        _db.Treasuries.AddRange(
            new Treasury
            {
                NameEn = "Main cash",
                NameAr = "الخزنة النقدية",
                Type = TreasuryType.Cash,
                OpeningBalance = 0m,
                IsActive = true,
                IsDefault = true,
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime
            },
            new Treasury
            {
                NameEn = "Main bank",
                NameAr = "الحساب البنكي",
                Type = TreasuryType.Bank,
                OpeningBalance = 0m,
                IsActive = true,
                IsDefault = false,
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime
            });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TreasuryListItemDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var query = _db.Treasuries.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.NameEn)
            .Select(x => new
            {
                x.Id,
                x.NameEn,
                x.NameAr,
                x.Type,
                x.OpeningBalance,
                x.IsActive,
                x.IsDefault,
                In = x.Transactions
                    .Where(t => t.Direction == TreasuryTransactionDirection.In)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                Out = x.Transactions
                    .Where(t => t.Direction == TreasuryTransactionDirection.Out)
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                MovementCount = x.Transactions.Count
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new TreasuryListItemDto
        {
            Id = x.Id,
            NameEn = x.NameEn,
            NameAr = x.NameAr,
            Type = x.Type,
            OpeningBalance = x.OpeningBalance,
            Balance = PricingCalculator.RoundMoney(x.OpeningBalance + x.In - x.Out),
            IsActive = x.IsActive,
            IsDefault = x.IsDefault,
            MovementCount = x.MovementCount
        }).ToList();
    }

    public async Task<TreasuryDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var list = await ListAsync(includeInactive: true, cancellationToken);
        var row = list.FirstOrDefault(x => x.Id == id);
        if (row is null)
        {
            return null;
        }

        var meta = await _db.Treasuries.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.CreatedAtUtc, x.UpdatedAtUtc })
            .FirstAsync(cancellationToken);

        return new TreasuryDetailDto
        {
            Id = row.Id,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Type = row.Type,
            OpeningBalance = row.OpeningBalance,
            Balance = row.Balance,
            IsActive = row.IsActive,
            IsDefault = row.IsDefault,
            MovementCount = row.MovementCount,
            CreatedAtUtc = meta.CreatedAtUtc,
            UpdatedAtUtc = meta.UpdatedAtUtc
        };
    }

    public async Task<TreasuryDetailDto> CreateAsync(TreasuryFormDto form, CancellationToken cancellationToken = default)
    {
        ValidateForm(form);
        await EnsureSeededAsync(cancellationToken);

        if (form.IsDefault)
        {
            await ClearDefaultsAsync(cancellationToken);
        }

        var entity = new Treasury
        {
            NameEn = form.NameEn.Trim(),
            NameAr = form.NameAr.Trim(),
            Type = form.Type,
            OpeningBalance = PricingCalculator.RoundMoney(form.OpeningBalance),
            IsActive = form.IsActive,
            IsDefault = form.IsDefault,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };
        _db.Treasuries.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TreasuryDetailDto> UpdateAsync(TreasuryFormDto form, CancellationToken cancellationToken = default)
    {
        if (!form.Id.HasValue)
        {
            throw new BusinessException(MsgNotFound);
        }

        ValidateForm(form);
        var entity = await _db.Treasuries.FirstOrDefaultAsync(x => x.Id == form.Id.Value, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        if (form.IsDefault && !entity.IsDefault)
        {
            await ClearDefaultsAsync(cancellationToken);
        }

        entity.NameEn = form.NameEn.Trim();
        entity.NameAr = form.NameAr.Trim();
        entity.Type = form.Type;
        entity.OpeningBalance = PricingCalculator.RoundMoney(form.OpeningBalance);
        entity.IsActive = form.IsActive;
        entity.IsDefault = form.IsDefault;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Treasuries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        entity.IsActive = false;
        entity.IsDefault = false;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Treasuries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        entity.IsActive = true;
        entity.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DepositAsync(TreasuryMoveDto dto, int userId, CancellationToken cancellationToken = default)
    {
        var amount = ValidateMove(dto);
        await EnsureUserExistsAsync(userId, cancellationToken);
        var treasury = await RequireActiveAsync(dto.TreasuryId, cancellationToken);
        AddLedger(
            treasury.Id,
            TreasuryTransactionDirection.In,
            amount,
            TreasuryTransactionSourceType.Manual,
            sourceId: null,
            userId,
            dto.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task WithdrawAsync(TreasuryMoveDto dto, int userId, CancellationToken cancellationToken = default)
    {
        var amount = ValidateMove(dto);
        await EnsureUserExistsAsync(userId, cancellationToken);
        var treasury = await RequireActiveAsync(dto.TreasuryId, cancellationToken);
        var balance = await GetBalanceAsync(treasury.Id, cancellationToken);
        if (balance < amount)
        {
            throw new BusinessException(MsgInsufficient, treasury.NameEn);
        }

        AddLedger(
            treasury.Id,
            TreasuryTransactionDirection.Out,
            amount,
            TreasuryTransactionSourceType.Manual,
            sourceId: null,
            userId,
            dto.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task TransferAsync(TreasuryTransferDto dto, int userId, CancellationToken cancellationToken = default)
    {
        if (dto.FromTreasuryId == dto.ToTreasuryId)
        {
            throw new BusinessException(MsgSameTreasury);
        }

        var amount = PricingCalculator.RoundMoney(dto.Amount);
        if (amount <= 0 || amount > MaxAmount)
        {
            throw new BusinessException(MsgAmount);
        }

        await EnsureUserExistsAsync(userId, cancellationToken);
        var from = await RequireActiveAsync(dto.FromTreasuryId, cancellationToken);
        var to = await RequireActiveAsync(dto.ToTreasuryId, cancellationToken);
        var balance = await GetBalanceAsync(from.Id, cancellationToken);
        if (balance < amount)
        {
            throw new BusinessException(MsgInsufficient, from.NameEn);
        }

        var groupId = Guid.NewGuid();
        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        var label = $"Transfer {from.NameEn} → {to.NameEn}";
        var fullNote = string.IsNullOrEmpty(note) ? label : $"{label}: {note}";

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            AddLedger(
                from.Id,
                TreasuryTransactionDirection.Out,
                amount,
                TreasuryTransactionSourceType.Transfer,
                sourceId: null,
                userId,
                fullNote,
                groupId);
            AddLedger(
                to.Id,
                TreasuryTransactionDirection.In,
                amount,
                TreasuryTransactionSourceType.Transfer,
                sourceId: null,
                userId,
                fullNote,
                groupId);
            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (tx is not null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task<TreasuryStatementDto> GetStatementAsync(
        TreasuryStatementFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var treasury = await GetAsync(filter.TreasuryId, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        var query = _db.TreasuryTransactions.AsNoTracking()
            .Where(x => x.TreasuryId == filter.TreasuryId);

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < filter.ToUtc.Value);
        }

        // Balance at From = opening + all ledger before From.
        var beforeQuery = _db.TreasuryTransactions.AsNoTracking()
            .Where(x => x.TreasuryId == filter.TreasuryId);
        if (filter.FromUtc.HasValue)
        {
            beforeQuery = beforeQuery.Where(x => x.CreatedAtUtc < filter.FromUtc.Value);
        }
        else
        {
            beforeQuery = beforeQuery.Where(_ => false);
        }

        var beforeIn = await beforeQuery
            .Where(x => x.Direction == TreasuryTransactionDirection.In)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var beforeOut = await beforeQuery
            .Where(x => x.Direction == TreasuryTransactionDirection.Out)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var openingAtFrom = filter.FromUtc.HasValue
            ? PricingCalculator.RoundMoney(treasury.OpeningBalance + beforeIn - beforeOut)
            : treasury.OpeningBalance;

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 5000);
        var rows = await query
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(take)
            .Select(x => new TreasuryTransactionDto
            {
                Id = x.Id,
                TreasuryId = x.TreasuryId,
                Direction = x.Direction,
                Amount = x.Amount,
                SourceType = x.SourceType,
                SourceId = x.SourceId,
                UserId = x.UserId,
                UserDisplayName = x.User.DisplayName,
                CreatedAtUtc = x.CreatedAtUtc,
                Note = x.Note,
                TransferGroupId = x.TransferGroupId
            })
            .ToListAsync(cancellationToken);

        var running = openingAtFrom;
        decimal totalIn = 0m, totalOut = 0m;
        foreach (var row in rows)
        {
            if (row.Direction == TreasuryTransactionDirection.In)
            {
                running = PricingCalculator.RoundMoney(running + row.Amount);
                totalIn += row.Amount;
            }
            else
            {
                running = PricingCalculator.RoundMoney(running - row.Amount);
                totalOut += row.Amount;
            }

            row.RunningBalance = running;
        }

        return new TreasuryStatementDto
        {
            Treasury = treasury,
            FromUtc = filter.FromUtc,
            ToUtc = filter.ToUtc,
            OpeningBalanceAtFrom = openingAtFrom,
            ClosingBalance = rows.Count == 0 ? openingAtFrom : running,
            TotalIn = PricingCalculator.RoundMoney(totalIn),
            TotalOut = PricingCalculator.RoundMoney(totalOut),
            Rows = rows
        };
    }

    public Task RecordShiftHandoverAsync(
        int treasuryId,
        decimal amount,
        int shiftId,
        int userId,
        CancellationToken cancellationToken = default) =>
        PostSystemAsync(
            treasuryId,
            TreasuryTransactionDirection.In,
            amount,
            TreasuryTransactionSourceType.ShiftClose,
            shiftId,
            userId,
            $"Shift #{shiftId} handover",
            allowZero: true,
            cancellationToken);

    public Task RecordSaleSettlementAsync(
        int treasuryId,
        decimal amount,
        int salesInvoiceId,
        int userId,
        string? note = null,
        CancellationToken cancellationToken = default) =>
        PostSystemAsync(
            treasuryId,
            TreasuryTransactionDirection.In,
            amount,
            TreasuryTransactionSourceType.Sale,
            salesInvoiceId,
            userId,
            note ?? $"Sale invoice #{salesInvoiceId}",
            allowZero: false,
            cancellationToken);

    public Task RecordPurchasePaymentAsync(
        int treasuryId,
        decimal amount,
        int purchaseInvoiceId,
        int userId,
        CancellationToken cancellationToken = default) =>
        PostSystemAsync(
            treasuryId,
            TreasuryTransactionDirection.Out,
            amount,
            TreasuryTransactionSourceType.Purchase,
            purchaseInvoiceId,
            userId,
            $"Purchase invoice #{purchaseInvoiceId}",
            allowZero: false,
            cancellationToken);

    private async Task PostSystemAsync(
        int treasuryId,
        TreasuryTransactionDirection direction,
        decimal amount,
        TreasuryTransactionSourceType sourceType,
        int sourceId,
        int userId,
        string note,
        bool allowZero,
        CancellationToken cancellationToken)
    {
        amount = PricingCalculator.RoundMoney(amount);
        if (amount < 0 || amount > MaxAmount || (!allowZero && amount <= 0))
        {
            throw new BusinessException(MsgAmount);
        }

        if (amount == 0)
        {
            return;
        }

        await EnsureUserExistsAsync(userId, cancellationToken);
        var treasury = await RequireActiveAsync(treasuryId, cancellationToken);

        if (direction == TreasuryTransactionDirection.Out)
        {
            var balance = await GetBalanceAsync(treasury.Id, cancellationToken);
            if (balance < amount)
            {
                throw new BusinessException(MsgInsufficient, treasury.NameEn);
            }
        }

        AddLedger(treasury.Id, direction, amount, sourceType, sourceId, userId, note);
        // Caller owns the ambient SaveChanges / transaction.
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void AddLedger(
        int treasuryId,
        TreasuryTransactionDirection direction,
        decimal amount,
        TreasuryTransactionSourceType sourceType,
        int? sourceId,
        int userId,
        string? note,
        Guid? transferGroupId = null)
    {
        _db.TreasuryTransactions.Add(new TreasuryTransaction
        {
            TreasuryId = treasuryId,
            Direction = direction,
            Amount = amount,
            SourceType = sourceType,
            SourceId = sourceId,
            UserId = userId,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            Note = note,
            TransferGroupId = transferGroupId
        });
    }

    private async Task<decimal> GetBalanceAsync(int treasuryId, CancellationToken cancellationToken)
    {
        var opening = await _db.Treasuries.AsNoTracking()
            .Where(x => x.Id == treasuryId)
            .Select(x => x.OpeningBalance)
            .FirstAsync(cancellationToken);
        var inn = await _db.TreasuryTransactions
            .Where(x => x.TreasuryId == treasuryId && x.Direction == TreasuryTransactionDirection.In)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outt = await _db.TreasuryTransactions
            .Where(x => x.TreasuryId == treasuryId && x.Direction == TreasuryTransactionDirection.Out)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return PricingCalculator.RoundMoney(opening + inn - outt);
    }

    private async Task<Treasury> RequireActiveAsync(int id, CancellationToken cancellationToken)
    {
        var row = await _db.Treasuries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        if (!row.IsActive)
        {
            throw new BusinessException(MsgInactive);
        }

        return row;
    }

    private async Task ClearDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = await _db.Treasuries.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var row in defaults)
        {
            row.IsDefault = false;
        }
    }

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        if (!await _db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
        {
            throw new BusinessException("User not found.");
        }
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionIfNeededAsync(
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    private static void ValidateForm(TreasuryFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.NameEn) || string.IsNullOrWhiteSpace(form.NameAr))
        {
            throw new BusinessException(MsgNameRequired);
        }

        if (form.OpeningBalance < 0 || form.OpeningBalance > MaxAmount)
        {
            throw new BusinessException(MsgAmount);
        }
    }

    private static decimal ValidateMove(TreasuryMoveDto dto)
    {
        var amount = PricingCalculator.RoundMoney(dto.Amount);
        if (amount <= 0 || amount > MaxAmount)
        {
            throw new BusinessException(MsgAmount);
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessException(MsgReasonRequired);
        }

        return amount;
    }
}
