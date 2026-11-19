using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class ChequeService : IChequeService
{
    public const decimal MaxAmount = 10_000_000m;

    public const string MsgNotFound = "Cheque not found.";
    public const string MsgNumberRequired = "Cheque number is required.";
    public const string MsgDueRequired = "Cheque due date is required.";
    public const string MsgAmount = "Amount must be greater than zero.";
    public const string MsgPartyRequired = "Choose a customer for incoming or a supplier for outgoing cheques.";
    public const string MsgPartyXor = "A cheque must be linked to exactly one customer or one supplier.";
    public const string MsgDirectionParty = "Incoming cheques need a customer; outgoing cheques need a supplier.";
    public const string MsgInvalidTransition = "That status change is not allowed.";
    public const string MsgTreasuryRequired = "Choose a treasury.";
    public const string MsgTerminal = "This cheque is already closed.";

    private readonly ApplicationDbContext _db;
    private readonly ITreasuryService _treasuries;
    private readonly TimeProvider _time;

    public ChequeService(ApplicationDbContext db, ITreasuryService treasuries, TimeProvider time)
    {
        _db = db;
        _treasuries = treasuries;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private DateTime TodayLocal
    {
        get
        {
            var zone = _time.LocalTimeZone;
            return TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).Date;
        }
    }

    public async Task<ChequeListResultDto> ListAsync(
        ChequeFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ChequeFilterDto();
        var today = TodayLocal;
        var query = ApplyFilter(_db.Cheques.AsNoTracking(), filter, today);

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 2000);
        var raw = await query
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Bank,
                x.DueDate,
                x.Amount,
                x.Direction,
                x.Status,
                x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.NameEn : null,
                x.SalesInvoiceId,
                SalesInvoiceNumber = x.SalesInvoice != null ? x.SalesInvoice.InvoiceNumber : null,
                x.PurchaseInvoiceId,
                PurchaseInvoiceNumber = x.PurchaseInvoice != null ? x.PurchaseInvoice.InvoiceNumber : null,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var rows = raw.Select(x => new ChequeListItemDto
        {
            Id = x.Id,
            Number = x.Number,
            Bank = x.Bank,
            DueDate = x.DueDate,
            Amount = x.Amount,
            Direction = x.Direction,
            Status = x.Status,
            CustomerId = x.CustomerId,
            CustomerName = x.CustomerName,
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            SalesInvoiceId = x.SalesInvoiceId,
            SalesInvoiceNumber = x.SalesInvoiceNumber,
            PurchaseInvoiceId = x.PurchaseInvoiceId,
            PurchaseInvoiceNumber = x.PurchaseInvoiceNumber,
            CreatedAtUtc = x.CreatedAtUtc,
            IsOverdue = x.Status == ChequeStatus.Pending && x.DueDate.Date < today
        }).ToList();

        var pending = rows.Where(x => x.Status == ChequeStatus.Pending).ToList();
        return new ChequeListResultDto
        {
            Items = rows,
            TotalAmount = PricingCalculator.RoundMoney(rows.Sum(x => x.Amount)),
            PendingAmount = PricingCalculator.RoundMoney(pending.Sum(x => x.Amount)),
            PendingCount = pending.Count,
            OverdueCount = pending.Count(x => x.IsOverdue)
        };
    }

    public async Task<ChequeDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var today = TodayLocal;
        var row = await _db.Cheques.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Bank,
                x.DueDate,
                x.Amount,
                x.Direction,
                x.Status,
                x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.NameEn : null,
                x.SalesInvoiceId,
                SalesInvoiceNumber = x.SalesInvoice != null ? x.SalesInvoice.InvoiceNumber : null,
                x.PurchaseInvoiceId,
                PurchaseInvoiceNumber = x.PurchaseInvoice != null ? x.PurchaseInvoice.InvoiceNumber : null,
                x.CreatedAtUtc,
                x.Notes,
                x.CreatedByUserId,
                CreatedBy = x.CreatedByUser.DisplayName,
                x.SettledTreasuryId,
                SettledTreasuryName = x.SettledTreasury != null ? x.SettledTreasury.NameEn : null,
                x.SettledAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var history = await _db.ChequeStatusHistory.AsNoTracking()
            .Where(x => x.ChequeId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new ChequeStatusHistoryDto
            {
                Status = x.Status,
                UserDisplayName = x.User.DisplayName,
                CreatedAtUtc = x.CreatedAtUtc,
                TreasuryName = x.Treasury != null ? x.Treasury.NameEn : null,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);

        return new ChequeDetailDto
        {
            Id = row.Id,
            Number = row.Number,
            Bank = row.Bank,
            DueDate = row.DueDate,
            Amount = row.Amount,
            Direction = row.Direction,
            Status = row.Status,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            SupplierId = row.SupplierId,
            SupplierName = row.SupplierName,
            SalesInvoiceId = row.SalesInvoiceId,
            SalesInvoiceNumber = row.SalesInvoiceNumber,
            PurchaseInvoiceId = row.PurchaseInvoiceId,
            PurchaseInvoiceNumber = row.PurchaseInvoiceNumber,
            CreatedAtUtc = row.CreatedAtUtc,
            IsOverdue = row.Status == ChequeStatus.Pending && row.DueDate.Date < today,
            Notes = row.Notes,
            CreatedByUserId = row.CreatedByUserId,
            CreatedByDisplayName = row.CreatedBy,
            SettledTreasuryId = row.SettledTreasuryId,
            SettledTreasuryName = row.SettledTreasuryName,
            SettledAtUtc = row.SettledAtUtc,
            History = history
        };
    }

    public Task<ChequeDetailDto> CreateAsync(
        ChequeFormDto form,
        int userId,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(form, userId, salesInvoiceId: null, purchaseInvoiceId: null, cancellationToken);

    public Task<ChequeDetailDto> CreateForSaleAsync(
        ChequeFormDto form,
        int salesInvoiceId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        form.Direction = ChequeDirection.In;
        form.SalesInvoiceId = salesInvoiceId;
        return CreateCoreAsync(form, userId, salesInvoiceId, purchaseInvoiceId: null, cancellationToken);
    }

    public Task<ChequeDetailDto> CreateForPurchaseAsync(
        ChequeFormDto form,
        int purchaseInvoiceId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        form.Direction = ChequeDirection.Out;
        form.PurchaseInvoiceId = purchaseInvoiceId;
        return CreateCoreAsync(form, userId, salesInvoiceId: null, purchaseInvoiceId, cancellationToken);
    }

    public async Task<ChequeDetailDto> ChangeStatusAsync(
        ChequeStatusChangeDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);
        var cheque = await _db.Cheques.FirstOrDefaultAsync(x => x.Id == dto.ChequeId, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        if (IsTerminal(cheque.Status))
        {
            throw new BusinessException(MsgTerminal);
        }

        if (cheque.Status != ChequeStatus.Pending)
        {
            throw new BusinessException(MsgInvalidTransition);
        }

        ValidateTransition(cheque.Direction, dto.NewStatus);

        var needsTreasury = dto.NewStatus is ChequeStatus.Collected or ChequeStatus.Paid;
        if (needsTreasury && (dto.TreasuryId is null or <= 0))
        {
            throw new BusinessException(MsgTreasuryRequired);
        }

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            if (dto.NewStatus == ChequeStatus.Collected)
            {
                await _treasuries.RecordChequeCollectedAsync(
                    dto.TreasuryId!.Value, cheque.Amount, cheque.Id, userId,
                    note: $"Cheque #{cheque.Number}", cancellationToken);
                cheque.SettledTreasuryId = dto.TreasuryId;
                cheque.SettledAtUtc = Now;
            }
            else if (dto.NewStatus == ChequeStatus.Paid)
            {
                await _treasuries.RecordChequePaidAsync(
                    dto.TreasuryId!.Value, cheque.Amount, cheque.Id, userId,
                    note: $"Cheque #{cheque.Number}", cancellationToken);
                cheque.SettledTreasuryId = dto.TreasuryId;
                cheque.SettledAtUtc = Now;
            }

            cheque.Status = dto.NewStatus;
            cheque.UpdatedAtUtc = Now;
            _db.ChequeStatusHistory.Add(new ChequeStatusHistory
            {
                ChequeId = cheque.Id,
                Status = dto.NewStatus,
                UserId = userId,
                CreatedAtUtc = Now,
                TreasuryId = needsTreasury ? dto.TreasuryId : null,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
            });

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

        return (await GetAsync(cheque.Id, cancellationToken))!;
    }

    public async Task<ChequeDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = TodayLocal;
        var weekEnd = today.AddDays(7);
        var pending = await _db.Cheques.AsNoTracking()
            .Where(x => x.Status == ChequeStatus.Pending)
            .Select(x => new { x.Direction, x.DueDate, x.Amount })
            .ToListAsync(cancellationToken);

        var incoming = pending.Where(x => x.Direction == ChequeDirection.In).ToList();
        var outgoing = pending.Where(x => x.Direction == ChequeDirection.Out).ToList();

        return new ChequeDashboardDto
        {
            IncomingDueToday = incoming.Count(x => x.DueDate.Date == today),
            IncomingDueThisWeek = incoming.Count(x => x.DueDate.Date >= today && x.DueDate.Date < weekEnd),
            IncomingOverdue = incoming.Count(x => x.DueDate.Date < today),
            IncomingOverdueAmount = PricingCalculator.RoundMoney(
                incoming.Where(x => x.DueDate.Date < today).Sum(x => x.Amount)),
            OutgoingDueToday = outgoing.Count(x => x.DueDate.Date == today),
            OutgoingDueThisWeek = outgoing.Count(x => x.DueDate.Date >= today && x.DueDate.Date < weekEnd),
            OutgoingOverdue = outgoing.Count(x => x.DueDate.Date < today),
            OutgoingOverdueAmount = PricingCalculator.RoundMoney(
                outgoing.Where(x => x.DueDate.Date < today).Sum(x => x.Amount))
        };
    }

    public async Task<ChequesReportDto> GetReportAsync(
        ChequesReportFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ChequesReportFilterDto();
        var today = TodayLocal;
        var listFilter = new ChequeFilterDto
        {
            Direction = filter.Direction,
            Status = filter.Status,
            DueFrom = filter.DueFrom,
            DueTo = filter.DueTo,
            Take = 2000
        };
        var result = await ListAsync(listFilter, cancellationToken);
        var byStatus = result.Items
            .GroupBy(x => x.Status)
            .Select(g => new NamedAmountDto
            {
                Name = g.Key.ToString(),
                Amount = PricingCalculator.RoundMoney(g.Sum(x => x.Amount)),
                Count = g.Count()
            })
            .OrderBy(x => x.Name)
            .ToList();

        return new ChequesReportDto
        {
            DueFrom = filter.DueFrom,
            DueTo = filter.DueTo,
            TotalAmount = result.TotalAmount,
            ByStatus = byStatus,
            Rows = result.Items
        };
    }

    public Task<IReadOnlyList<ChequeListItemDto>> ListForCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default) =>
        ListPartyAsync(customerId: customerId, supplierId: null, cancellationToken);

    public Task<IReadOnlyList<ChequeListItemDto>> ListForSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default) =>
        ListPartyAsync(customerId: null, supplierId: supplierId, cancellationToken);

    public async Task<decimal> OutstandingTotalForCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default) =>
        await _db.Cheques.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Status == ChequeStatus.Pending)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public async Task<decimal> OutstandingTotalForSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default) =>
        await _db.Cheques.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.Status == ChequeStatus.Pending)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public bool IsTerminal(ChequeStatus status) =>
        status is ChequeStatus.Collected or ChequeStatus.Paid or ChequeStatus.Bounced or ChequeStatus.Cancelled;

    private async Task<ChequeDetailDto> CreateCoreAsync(
        ChequeFormDto form,
        int userId,
        int? salesInvoiceId,
        int? purchaseInvoiceId,
        CancellationToken cancellationToken)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);
        ValidateForm(form);

        var entity = new Cheque
        {
            Number = form.Number.Trim(),
            Bank = string.IsNullOrWhiteSpace(form.Bank) ? null : form.Bank.Trim(),
            DueDate = form.DueDate.Date,
            Amount = PricingCalculator.RoundMoney(form.Amount),
            Direction = form.Direction,
            Status = ChequeStatus.Pending,
            CustomerId = form.Direction == ChequeDirection.In ? form.CustomerId : null,
            SupplierId = form.Direction == ChequeDirection.Out ? form.SupplierId : null,
            SalesInvoiceId = salesInvoiceId ?? form.SalesInvoiceId,
            PurchaseInvoiceId = purchaseInvoiceId ?? form.PurchaseInvoiceId,
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
            CreatedByUserId = userId,
            CreatedAtUtc = Now
        };

        _db.Cheques.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ChequeStatusHistory.Add(new ChequeStatusHistory
        {
            ChequeId = entity.Id,
            Status = ChequeStatus.Pending,
            UserId = userId,
            CreatedAtUtc = Now,
            Note = "Created"
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    private async Task<IReadOnlyList<ChequeListItemDto>> ListPartyAsync(
        int? customerId,
        int? supplierId,
        CancellationToken cancellationToken)
    {
        var today = TodayLocal;
        var query = _db.Cheques.AsNoTracking().AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == supplierId.Value);
        }

        var raw = await query
            .OrderByDescending(x => x.DueDate)
            .Take(200)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Bank,
                x.DueDate,
                x.Amount,
                x.Direction,
                x.Status,
                x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.NameEn : null,
                x.SalesInvoiceId,
                SalesInvoiceNumber = x.SalesInvoice != null ? x.SalesInvoice.InvoiceNumber : null,
                x.PurchaseInvoiceId,
                PurchaseInvoiceNumber = x.PurchaseInvoice != null ? x.PurchaseInvoice.InvoiceNumber : null,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return raw.Select(x => new ChequeListItemDto
        {
            Id = x.Id,
            Number = x.Number,
            Bank = x.Bank,
            DueDate = x.DueDate,
            Amount = x.Amount,
            Direction = x.Direction,
            Status = x.Status,
            CustomerId = x.CustomerId,
            CustomerName = x.CustomerName,
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            SalesInvoiceId = x.SalesInvoiceId,
            SalesInvoiceNumber = x.SalesInvoiceNumber,
            PurchaseInvoiceId = x.PurchaseInvoiceId,
            PurchaseInvoiceNumber = x.PurchaseInvoiceNumber,
            CreatedAtUtc = x.CreatedAtUtc,
            IsOverdue = x.Status == ChequeStatus.Pending && x.DueDate.Date < today
        }).ToList();
    }

    private static IQueryable<Cheque> ApplyFilter(
        IQueryable<Cheque> query,
        ChequeFilterDto filter,
        DateTime today)
    {
        if (filter.Direction.HasValue)
        {
            query = query.Where(x => x.Direction == filter.Direction.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
        }

        if (filter.SupplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == filter.SupplierId.Value);
        }

        if (filter.DueFrom.HasValue)
        {
            query = query.Where(x => x.DueDate >= filter.DueFrom.Value.Date);
        }

        if (filter.DueTo.HasValue)
        {
            query = query.Where(x => x.DueDate < filter.DueTo.Value.Date.AddDays(1));
        }

        if (filter.OverdueOnly)
        {
            query = query.Where(x => x.Status == ChequeStatus.Pending && x.DueDate < today);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.Number.Contains(term)
                || (x.Bank != null && x.Bank.Contains(term))
                || (x.Customer != null && (x.Customer.Name.Contains(term) || x.Customer.Name.Contains(term)))
                || (x.Supplier != null && (x.Supplier.NameEn.Contains(term) || x.Supplier.NameAr.Contains(term))));
        }

        return query;
    }

    private static void ValidateForm(ChequeFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Number))
        {
            throw new BusinessException(MsgNumberRequired);
        }

        if (form.DueDate == default)
        {
            throw new BusinessException(MsgDueRequired);
        }

        var amount = PricingCalculator.RoundMoney(form.Amount);
        if (amount <= 0 || amount > MaxAmount)
        {
            throw new BusinessException(MsgAmount);
        }

        var hasCustomer = form.CustomerId is > 0;
        var hasSupplier = form.SupplierId is > 0;
        if (hasCustomer == hasSupplier)
        {
            throw new BusinessException(hasCustomer ? MsgPartyXor : MsgPartyRequired);
        }

        if (form.Direction == ChequeDirection.In && !hasCustomer)
        {
            throw new BusinessException(MsgDirectionParty);
        }

        if (form.Direction == ChequeDirection.Out && !hasSupplier)
        {
            throw new BusinessException(MsgDirectionParty);
        }
    }

    private static void ValidateTransition(ChequeDirection direction, ChequeStatus next)
    {
        var ok = direction switch
        {
            ChequeDirection.In => next is ChequeStatus.Collected or ChequeStatus.Bounced or ChequeStatus.Cancelled,
            ChequeDirection.Out => next is ChequeStatus.Paid or ChequeStatus.Cancelled,
            _ => false
        };
        if (!ok)
        {
            throw new BusinessException(MsgInvalidTransition);
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
}
