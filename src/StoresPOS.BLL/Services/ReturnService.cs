using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class ReturnService : IReturnService
{
    public const int MaxReasonLength = 300;

    public const string MsgNoPermission = "You do not have permission to make returns.";
    public const string MsgInvoiceNotFound = "Invoice not found.";
    public const string MsgOutsideWindow = "This invoice is older than {0} days and can no longer be returned.";
    public const string MsgNoLines = "Choose at least one item to return.";
    public const string MsgLineNotOnInvoice = "That item is not on this invoice.";
    public const string MsgInvalidQuantity = "Return quantity must be greater than zero.";
    public const string MsgTooMany = "Only {0} of \"{1}\" can still be returned.";
    public const string MsgInvalidMethod = "Refund in cash or by the invoice's original payment method.";
    public const string MsgNoShiftForCash = "A cash refund needs an open shift — the money comes out of the drawer.";
    public const string MsgReasonRequired = "Enter the reason for this return.";
    public const string MsgReasonTooLong = "The reason is too long (300 characters max).";
    public const string MsgSaveFailed = "The return could not be saved. Try again.";

    private readonly ApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IPermissionService _permissions;
    private readonly IPaymentMethodService _paymentMethods;
    private readonly IWarehouseService _warehouses;
    private readonly StoreSettings _store;
    private readonly TimeProvider _time;

    public ReturnService(
        ApplicationDbContext db,
        IInventoryService inventory,
        IPermissionService permissions,
        IPaymentMethodService paymentMethods,
        IWarehouseService warehouses,
        IOptions<StoreSettings> store,
        TimeProvider time)
    {
        _db = db;
        _inventory = inventory;
        _permissions = permissions;
        _paymentMethods = paymentMethods;
        _warehouses = warehouses;
        _store = store.Value;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public static string FormatReturnNumber(int sequence) =>
        "R-" + sequence.ToString("D6", CultureInfo.InvariantCulture);

    public async Task<int?> FindInvoiceIdAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var number = (invoiceNumber ?? string.Empty).Trim().ToUpperInvariant();
        if (number.Length == 0)
        {
            return null;
        }

        // A bare number ("15") is accepted as S-000015.
        if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
        {
            number = SalesService.FormatInvoiceNumber(seq);
        }

        return await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.Status == SalesInvoiceStatus.Completed && x.InvoiceNumber == number)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ReturnableInvoiceDto?> GetReturnableAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.CashierUser)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.Status == SalesInvoiceStatus.Completed, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var returned = await ReturnedQuantitiesAsync(invoiceId, cancellationToken);
        var factor = DiscountFactor(invoice);
        var previous = await GetListAsync(new SalesReturnFilterDto { InvoiceId = invoiceId }, cancellationToken);

        return new ReturnableInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber!,
            CompletedAtUtc = invoice.CompletedAtUtc!.Value,
            CashierDisplayName = invoice.CashierUser.DisplayName,
            PaymentMethod = invoice.PaymentMethod!.Value,
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            Total = invoice.Total,
            RefundedSoFar = previous.Sum(x => x.TotalAmount),
            Lines = invoice.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new ReturnableLineDto
                {
                    SalesInvoiceItemId = x.Id,
                    ProductId = x.ProductId,
                    NameAr = x.ProductNameAr,
                    NameEn = x.ProductNameEn,
                    Unit = x.Unit,
                    SoldQuantity = x.Quantity,
                    ReturnedQuantity = returned.GetValueOrDefault(x.Id),
                    UnitPrice = x.UnitPrice,
                    RefundUnitPrice = PricingCalculator.RoundMoney(x.UnitPrice * factor)
                })
                .ToList(),
            PreviousReturns = previous,
            ReturnWindowDays = _store.ReturnWindowDays,
            IsWithinWindow = IsWithinWindow(invoice.CompletedAtUtc!.Value),
            HasOpenShift = await _db.Shifts.AnyAsync(x => x.Status == ShiftStatus.Open, cancellationToken)
        };
    }

    public async Task<SalesReturnDetailsDto> CreateAsync(
        CreateReturnDto dto,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await _permissions.HasPermissionAsync(userId, AppPermissions.ReturnsCreate, cancellationToken))
        {
            throw new BusinessException(MsgNoPermission);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var id = await CreateOnceAsync(dto, userId, cancellationToken);
                return (await GetAsync(id, cancellationToken))!;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // Another till took the same return number at the same moment — retry with a fresh one.
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                throw new BusinessException(MsgSaveFailed);
            }
        }
    }

    private async Task<int> CreateOnceAsync(CreateReturnDto dto, int userId, CancellationToken cancellationToken)
    {
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new BusinessException(MsgReasonRequired);
        }

        if (reason.Length > MaxReasonLength)
        {
            throw new BusinessException(MsgReasonTooLong);
        }

        var wanted = (dto.Lines ?? [])
            .Where(x => x.Quantity != 0)
            .GroupBy(x => x.SalesInvoiceItemId)
            .Select(g => new { ItemId = g.Key, Quantity = Math.Round(g.Sum(x => x.Quantity), 3, MidpointRounding.AwayFromZero) })
            .ToList();
        if (wanted.Count == 0)
        {
            throw new BusinessException(MsgNoLines);
        }

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var invoice = await _db.SalesInvoices
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == dto.InvoiceId && x.Status == SalesInvoiceStatus.Completed, cancellationToken)
                ?? throw new BusinessException(MsgInvoiceNotFound);

            if (!IsWithinWindow(invoice.CompletedAtUtc!.Value))
            {
                throw new BusinessException(MsgOutsideWindow, _store.ReturnWindowDays);
            }

            var method = dto.RefundMethod;
            var methodDef = await _paymentMethods.GetAsync((byte)method, cancellationToken);
            var isCashRefund = methodDef?.IsCash == true;
            if (methodDef is null || (!isCashRefund && method != invoice.PaymentMethod))
            {
                throw new BusinessException(MsgInvalidMethod);
            }

            var openShiftId = await _db.Shifts
                .Where(x => x.Status == ShiftStatus.Open)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (isCashRefund && openShiftId is null)
            {
                throw new BusinessException(MsgNoShiftForCash);
            }

            var returned = await ReturnedQuantitiesAsync(invoice.Id, cancellationToken);
            var factor = DiscountFactor(invoice);

            var salesReturn = new SalesReturn
            {
                OriginalSalesInvoiceId = invoice.Id,
                ShiftId = openShiftId,
                UserId = userId,
                RefundMethod = method,
                Reason = reason,
                CreatedAtUtc = Now
            };

            var sortedItems = invoice.Items.OrderBy(x => x.SortOrder).ToList();
            foreach (var item in sortedItems)
            {
                var line = wanted.FirstOrDefault(x => x.ItemId == item.Id);
                if (line is null)
                {
                    continue;
                }

                if (line.Quantity <= 0)
                {
                    throw new BusinessException(MsgInvalidQuantity);
                }

                var available = item.Quantity - returned.GetValueOrDefault(item.Id);
                if (line.Quantity > available)
                {
                    throw new BusinessException(MsgTooMany, Qty(Math.Max(0m, available)), item.ProductNameEn);
                }

                salesReturn.Items.Add(new SalesReturnItem
                {
                    SalesInvoiceItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductNameAr = item.ProductNameAr,
                    ProductNameEn = item.ProductNameEn,
                    Unit = item.Unit,
                    Quantity = line.Quantity,
                    UnitPrice = item.UnitPrice,
                    RefundAmount = PricingCalculator.RoundMoney(line.Quantity * item.UnitPrice * factor)
                });
            }

            if (wanted.Any(w => sortedItems.All(i => i.Id != w.ItemId)))
            {
                throw new BusinessException(MsgLineNotOnInvoice);
            }

            // Never refund more than the invoice took. When this return empties the invoice, the last line
            // absorbs the rounding so all refunds add up to exactly the invoice total.
            var refundedSoFar = await _db.SalesReturns
                .Where(x => x.OriginalSalesInvoiceId == invoice.Id)
                .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
            var remaining = invoice.Total - refundedSoFar;
            var total = salesReturn.Items.Sum(x => x.RefundAmount);
            var emptiesInvoice = sortedItems.All(i =>
                i.Quantity - returned.GetValueOrDefault(i.Id)
                - salesReturn.Items.Where(r => r.SalesInvoiceItemId == i.Id).Sum(r => r.Quantity) <= 0);
            if (emptiesInvoice || total > remaining)
            {
                var last = salesReturn.Items.Last();
                last.RefundAmount = Math.Max(0m, last.RefundAmount + (remaining - total));
                total = salesReturn.Items.Sum(x => x.RefundAmount);
            }

            salesReturn.TotalAmount = total;

            var sequence = (await _db.SalesReturns.MaxAsync(x => (int?)x.ReturnSequence, cancellationToken) ?? 0) + 1;
            salesReturn.ReturnSequence = sequence;
            salesReturn.ReturnNumber = FormatReturnNumber(sequence);

            _db.SalesReturns.Add(salesReturn);
            await _db.SaveChangesAsync(cancellationToken);

            var sellingWarehouseId = await _warehouses.GetSellingWarehouseIdAsync(cancellationToken);
            await _inventory.ApplySaleReturnAsync(
                salesReturn.Id,
                sellingWarehouseId,
                salesReturn.Items.Select(x => new SaleStockLineDto { ProductId = x.ProductId, Quantity = x.Quantity }).ToList(),
                userId,
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return salesReturn.Id;
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

    public async Task<IReadOnlyList<SalesReturnListItemDto>> GetListAsync(
        SalesReturnFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SalesReturnFilterDto();
        var query = _db.SalesReturns.AsNoTracking();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < filter.ToUtc.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(x => x.UserId == filter.UserId.Value);
        }

        if (filter.ShiftId.HasValue)
        {
            query = query.Where(x => x.ShiftId == filter.ShiftId.Value);
        }

        if (filter.InvoiceId.HasValue)
        {
            query = query.Where(x => x.OriginalSalesInvoiceId == filter.InvoiceId.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(x => x.OriginalSalesInvoice.CustomerId == filter.CustomerId.Value);
        }

        if (filter.RefundMethod.HasValue)
        {
            query = query.Where(x => x.RefundMethod == filter.RefundMethod.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.ReturnNumber.Contains(term)
                || (x.OriginalSalesInvoice.InvoiceNumber != null && x.OriginalSalesInvoice.InvoiceNumber.Contains(term)));
        }

        var take = filter.Take <= 0 ? 500 : Math.Min(filter.Take, 2000);
        return await query
            .OrderByDescending(x => x.ReturnSequence)
            .Take(take)
            .Select(x => new SalesReturnListItemDto
            {
                Id = x.Id,
                ReturnNumber = x.ReturnNumber,
                OriginalInvoiceId = x.OriginalSalesInvoiceId,
                OriginalInvoiceNumber = x.OriginalSalesInvoice.InvoiceNumber!,
                CreatedAtUtc = x.CreatedAtUtc,
                ShiftId = x.ShiftId,
                UserDisplayName = x.User.DisplayName,
                RefundMethod = x.RefundMethod,
                TotalAmount = x.TotalAmount,
                Reason = x.Reason
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesReturnDetailsDto?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.SalesReturns
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SalesReturnDetailsDto
            {
                Id = x.Id,
                ReturnNumber = x.ReturnNumber,
                OriginalInvoiceId = x.OriginalSalesInvoiceId,
                OriginalInvoiceNumber = x.OriginalSalesInvoice.InvoiceNumber!,
                CreatedAtUtc = x.CreatedAtUtc,
                ShiftId = x.ShiftId,
                UserDisplayName = x.User.DisplayName,
                RefundMethod = x.RefundMethod,
                TotalAmount = x.TotalAmount,
                Reason = x.Reason,
                Lines = x.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new SalesReturnLineDto
                    {
                        NameAr = i.ProductNameAr,
                        NameEn = i.ProductNameEn,
                        Unit = i.Unit,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        RefundAmount = i.RefundAmount
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

    // ---------------------------------------------------------------- helpers

    private bool IsWithinWindow(DateTime completedAtUtc) =>
        _store.ReturnWindowDays <= 0 || completedAtUtc.AddDays(_store.ReturnWindowDays) >= Now;

    /// <summary>Share of each line that was actually paid after the invoice discount (Total / Subtotal).</summary>
    private static decimal DiscountFactor(SalesInvoice invoice) =>
        invoice.Subtotal > 0 ? invoice.Total / invoice.Subtotal : 0m;

    private async Task<Dictionary<int, decimal>> ReturnedQuantitiesAsync(int invoiceId, CancellationToken cancellationToken) =>
        await _db.SalesReturnItems
            .AsNoTracking()
            .Where(x => x.SalesReturn.OriginalSalesInvoiceId == invoiceId)
            .GroupBy(x => x.SalesInvoiceItemId)
            .Select(g => new { g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity, cancellationToken);

    private static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }
}
