using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class SalesService : ISalesService
{
    public const decimal MaxQuantity = 100_000m;
    public const decimal MaxMoney = 10_000_000m;

    public const string MsgNotInShift = "Open or join the shift before selling.";
    public const string MsgProductNotFound = "Product not found.";
    public const string MsgBarcodeNotFound = "No product with barcode \"{0}\".";
    public const string MsgProductInactive = "\"{0}\" is no longer for sale.";
    public const string MsgOutOfStock = "\"{0}\" is out of stock.";
    public const string MsgNotEnoughStock = "Only {0} left of \"{1}\". Lower the quantity to {0} or less.";
    public const string MsgInvalidQuantity = "Quantity must be greater than zero.";
    public const string MsgLineNotFound = "That line is no longer on the invoice.";
    public const string MsgNoPricePermission = "You do not have permission to change prices.";
    public const string MsgInvalidPrice = "Price must be greater than zero.";
    public const string MsgNoDiscountPermission = "You do not have permission to give discounts.";
    public const string MsgInvalidDiscount = "Discount must be between 0 and the invoice subtotal.";
    public const string MsgEmptyInvoice = "The invoice is empty. Add at least one item.";
    public const string MsgPaidTooLow = "Paid {0} is less than the total {1}.";
    public const string MsgInvalidPaid = "Enter a valid amount paid.";
    public const string MsgFinishCurrentFirst = "Hold or finish the current invoice first.";
    public const string MsgHeldNotFound = "That held invoice is no longer available.";
    public const string MsgNoClosePermission = "You do not have permission to close the shift.";
    public const string MsgSaveFailed = "The invoice could not be saved. Try again.";
    public const string MsgCustomerNotFound = "That customer is not available. Pick another one.";

    public const string MsgInvalidPaymentMethod = "That payment method is not available.";
    public const string MsgChequeNeedsCustomer = "Pick a customer before paying by cheque.";
    public const string MsgChequeNumberRequired = "Cheque number is required.";
    public const string MsgChequeDueRequired = "Cheque due date is required.";

    private readonly ApplicationDbContext _db;
    private readonly IShiftService _shifts;
    private readonly IInventoryService _inventory;
    private readonly IPermissionService _permissions;
    private readonly IPaymentMethodService _paymentMethods;
    private readonly ITreasuryService _treasuries;
    private readonly IChequeService _cheques;
    private readonly IWarehouseService _warehouses;
    private readonly TimeProvider _time;

    public SalesService(
        ApplicationDbContext db,
        IShiftService shifts,
        IInventoryService inventory,
        IPermissionService permissions,
        IPaymentMethodService paymentMethods,
        ITreasuryService treasuries,
        IChequeService cheques,
        IWarehouseService warehouses,
        TimeProvider time)
    {
        _db = db;
        _shifts = shifts;
        _inventory = inventory;
        _permissions = permissions;
        _paymentMethods = paymentMethods;
        _treasuries = treasuries;
        _cheques = cheques;
        _warehouses = warehouses;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- catalog

    public async Task<IReadOnlyList<PosCategoryDto>> GetPosCategoriesAsync(CancellationToken cancellationToken = default) =>
        await _db.Categories
            .AsNoTracking()
            .Where(x => x.IsActive && x.ParentCategoryId == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .Select(x => new PosCategoryDto { Id = x.Id, NameAr = x.NameAr, NameEn = x.NameEn })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(
        PosSearchDto search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking().Where(x => x.IsActive);

        if (search.CategoryId.HasValue)
        {
            var ids = await GetCategoryWithDescendantsAsync(search.CategoryId.Value, cancellationToken);
            query = query.Where(x => x.CategoryId != null && ids.Contains(x.CategoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search.Term))
        {
            var term = search.Term.Trim();
            query = query.Where(x =>
                x.NameAr.Contains(term)
                || x.NameEn.Contains(term)
                || (x.Barcode != null && x.Barcode.Contains(term)));
        }

        var wholesale = search.SaleType == SaleType.Wholesale;
        var take = search.Take <= 0 ? 60 : Math.Min(search.Take, 200);
        var sellingWarehouseId = await _warehouses.GetSellingWarehouseIdAsync(cancellationToken);

        var rows = await query
            .OrderBy(x => x.NameEn)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.NameAr,
                x.NameEn,
                x.Barcode,
                x.Unit,
                Price = wholesale ? x.WholesalePrice : x.RetailPrice,
                x.LowStockThreshold,
                WarehouseQty = _db.WarehouseStocks
                    .Where(ws => ws.WarehouseId == sellingWarehouseId && ws.ProductId == x.Id)
                    .Select(ws => (decimal?)ws.Quantity)
                    .FirstOrDefault() ?? 0m
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new PosProductDto
        {
            Id = x.Id,
            NameAr = x.NameAr,
            NameEn = x.NameEn,
            Barcode = x.Barcode,
            Unit = x.Unit,
            Price = x.Price,
            StockQuantity = x.WarehouseQty,
            LowStockThreshold = x.LowStockThreshold
        }).ToList();
    }

    // ---------------------------------------------------------------- cart

    public async Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        return await ToCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> ScanAsync(int userId, string barcode, CancellationToken cancellationToken = default)
    {
        var code = (barcode ?? string.Empty).Trim();
        var productId = await _db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.Barcode == code)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(MsgBarcodeNotFound, code);

        return await AddProductAsync(userId, productId, 1m, cancellationToken);
    }

    public async Task<CartDto> AddProductAsync(
        int userId,
        int productId,
        decimal quantity = 1m,
        CancellationToken cancellationToken = default)
    {
        var qty = ValidateQuantity(quantity);
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new BusinessException(MsgProductNotFound);

        if (!product.IsActive)
        {
            throw new BusinessException(MsgProductInactive, product.NameEn);
        }

        var line = draft.Items.FirstOrDefault(x => x.ProductId == productId);
        var newQty = (line?.Quantity ?? 0m) + qty;
        await EnsureSellingStockAsync(product, newQty, cancellationToken);

        if (line is null)
        {
            var price = CatalogPrice(product, draft.SaleType);
            draft.Items.Add(new SalesInvoiceItem
            {
                ProductId = product.Id,
                ProductNameAr = product.NameAr,
                ProductNameEn = product.NameEn,
                Barcode = product.Barcode,
                Unit = product.Unit,
                Quantity = newQty,
                CatalogUnitPrice = price,
                UnitPrice = price,
                UnitCost = product.PurchasePrice,
                SortOrder = draft.Items.Count == 0 ? 1 : draft.Items.Max(x => x.SortOrder) + 1
            });
        }
        else
        {
            line.Quantity = newQty;
        }

        return await SaveCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> SetQuantityAsync(
        int userId,
        int lineId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        var line = FindLine(draft, lineId);

        if (quantity <= 0)
        {
            draft.Items.Remove(line);
            _db.SalesInvoiceItems.Remove(line);
            return await SaveCartAsync(draft, cancellationToken);
        }

        var qty = ValidateQuantity(quantity);
        var product = await _db.Products.AsNoTracking().FirstAsync(x => x.Id == line.ProductId, cancellationToken);
        await EnsureSellingStockAsync(product, qty, cancellationToken);
        line.Quantity = qty;
        return await SaveCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> ChangeQuantityAsync(
        int userId,
        int lineId,
        decimal delta,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        var line = FindLine(draft, lineId);
        return await SetQuantityAsync(userId, lineId, line.Quantity + delta, cancellationToken);
    }

    public Task<CartDto> RemoveLineAsync(int userId, int lineId, CancellationToken cancellationToken = default) =>
        SetQuantityAsync(userId, lineId, 0m, cancellationToken);

    public async Task<CartDto> SetSaleTypeAsync(int userId, SaleType saleType, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        await ApplySaleTypeAsync(draft, saleType, cancellationToken);
        return await SaveCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> SetCustomerAsync(int userId, int? customerId, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        if (customerId is null or <= 0)
        {
            draft.CustomerId = null;
            return await SaveCartAsync(draft, cancellationToken);
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId.Value && x.IsActive, cancellationToken)
            ?? throw new BusinessException(MsgCustomerNotFound);

        draft.CustomerId = customer.Id;
        await ApplySaleTypeAsync(draft, customer.DefaultSaleType, cancellationToken);
        return await SaveCartAsync(draft, cancellationToken);
    }

    private async Task ApplySaleTypeAsync(SalesInvoice draft, SaleType saleType, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(saleType))
        {
            saleType = SaleType.Retail;
        }

        if (draft.SaleType != saleType)
        {
            draft.SaleType = saleType;
            var ids = draft.Items.Select(x => x.ProductId).ToList();
            var products = await _db.Products.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var line in draft.Items)
            {
                var catalog = CatalogPrice(products[line.ProductId], saleType);
                line.CatalogUnitPrice = catalog;

                // Manually edited prices stay as the cashier set them.
                if (!line.IsPriceOverridden)
                {
                    line.UnitPrice = catalog;
                }
            }
        }
    }

    public async Task<CartDto> OverridePriceAsync(
        int userId,
        int lineId,
        decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        if (!await _permissions.HasPermissionAsync(userId, AppPermissions.SalesPriceOverride, cancellationToken))
        {
            throw new BusinessException(MsgNoPricePermission);
        }

        var price = PricingCalculator.RoundMoney(unitPrice);
        if (price <= 0 || price > MaxMoney)
        {
            throw new BusinessException(MsgInvalidPrice);
        }

        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        var line = FindLine(draft, lineId);

        if (price == line.CatalogUnitPrice)
        {
            line.UnitPrice = price;
            line.IsPriceOverridden = false;
            line.PriceOverriddenByUserId = null;
        }
        else
        {
            line.UnitPrice = price;
            line.IsPriceOverridden = true;
            line.PriceOverriddenByUserId = userId;
        }

        return await SaveCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> SetDiscountAsync(
        int userId,
        DiscountDto discount,
        CancellationToken cancellationToken = default)
    {
        if (!await _permissions.HasPermissionAsync(userId, AppPermissions.SalesDiscount, cancellationToken))
        {
            throw new BusinessException(MsgNoDiscountPermission);
        }

        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        var subtotal = draft.Items.Sum(x => PricingCalculator.RoundMoney(x.Quantity * x.UnitPrice));

        decimal amount;
        if (discount.Percent.HasValue)
        {
            if (discount.Percent < 0 || discount.Percent > 100)
            {
                throw new BusinessException(MsgInvalidDiscount);
            }

            amount = PricingCalculator.RoundMoney(subtotal * discount.Percent.Value / 100m);
        }
        else
        {
            amount = PricingCalculator.RoundMoney(discount.Amount ?? 0m);
        }

        if (amount < 0 || amount > subtotal)
        {
            throw new BusinessException(MsgInvalidDiscount);
        }

        draft.DiscountAmount = amount;
        draft.DiscountByUserId = amount > 0 ? userId : null;
        return await SaveCartAsync(draft, cancellationToken);
    }

    public async Task<CartDto> ClearAsync(int userId, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        _db.SalesInvoiceItems.RemoveRange(draft.Items);
        draft.Items.Clear();
        draft.DiscountAmount = 0;
        draft.DiscountByUserId = null;
        draft.CustomerId = null;
        return await SaveCartAsync(draft, cancellationToken);
    }

    // ---------------------------------------------------------------- hold / resume

    public async Task<CartDto> HoldAsync(int userId, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        if (draft.Items.Count == 0)
        {
            throw new BusinessException(MsgEmptyInvoice);
        }

        draft.Status = SalesInvoiceStatus.Held;
        draft.HeldAtUtc = Now;
        draft.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<HeldInvoiceDto>> GetHeldAsync(int userId, CancellationToken cancellationToken = default)
    {
        var shiftId = await RequireShiftAsync(userId, cancellationToken);
        return await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId && x.Status == SalesInvoiceStatus.Held)
            .OrderBy(x => x.HeldAtUtc)
            .Select(x => new HeldInvoiceDto
            {
                Id = x.Id,
                CashierDisplayName = x.CashierUser.DisplayName,
                HeldAtUtc = x.HeldAtUtc ?? x.UpdatedAtUtc,
                LineCount = x.Items.Count,
                Total = x.Total,
                SaleType = x.SaleType,
                CustomerName = x.Customer != null ? x.Customer.Name : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CartDto> ResumeHeldAsync(int userId, int invoiceId, CancellationToken cancellationToken = default)
    {
        var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
        if (draft.Items.Count > 0)
        {
            throw new BusinessException(MsgFinishCurrentFirst);
        }

        var held = await _db.SalesInvoices
            .FirstOrDefaultAsync(x => x.Id == invoiceId
                                      && x.ShiftId == draft.ShiftId
                                      && x.Status == SalesInvoiceStatus.Held, cancellationToken)
            ?? throw new BusinessException(MsgHeldNotFound);

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);

        // Free the one-draft-per-cashier slot before the held invoice takes it.
        _db.SalesInvoices.Remove(draft);
        await _db.SaveChangesAsync(cancellationToken);

        held.Status = SalesInvoiceStatus.Draft;
        held.CashierUserId = userId;
        held.HeldAtUtc = null;
        held.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(cancellationToken);

        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return await GetCartAsync(userId, cancellationToken);
    }

    public async Task HoldForCashierAsync(int actorUserId, int invoiceId, CancellationToken cancellationToken = default)
    {
        if (!await _permissions.HasPermissionAsync(actorUserId, AppPermissions.ShiftsClose, cancellationToken))
        {
            throw new BusinessException(MsgNoClosePermission);
        }

        var draft = await _db.SalesInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.Status == SalesInvoiceStatus.Draft, cancellationToken);

        if (draft is null || draft.Items.Count == 0)
        {
            return; // already paid, held or emptied — nothing blocks the close any more
        }

        draft.Status = SalesInvoiceStatus.Held;
        draft.HeldAtUtc = Now;
        draft.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- complete

    public async Task<CompletedSaleDto> CompleteAsync(
        int userId,
        PaymentDto payment,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CompleteOnceAsync(userId, payment, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // Another till took the same invoice sequence at the same moment — retry with a fresh number.
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                throw new BusinessException(MsgSaveFailed);
            }
        }
    }

    private async Task<CompletedSaleDto> CompleteOnceAsync(
        int userId,
        PaymentDto payment,
        CancellationToken cancellationToken)
    {
        var methodDef = await _paymentMethods.GetAsync((byte)payment.Method, cancellationToken);
        if (methodDef is null || !methodDef.IsActive)
        {
            throw new BusinessException(MsgInvalidPaymentMethod);
        }

        var method = (PaymentMethod)methodDef.Id;
        var isCash = methodDef.IsCash;

        await using var tx = await BeginTransactionIfNeededAsync(cancellationToken);
        try
        {
            var draft = await GetOrCreateDraftAsync(userId, cancellationToken);
            if (draft.Items.Count == 0)
            {
                throw new BusinessException(MsgEmptyInvoice);
            }

            Recalculate(draft);

            decimal paid;
            decimal change;
            if (isCash)
            {
                paid = PricingCalculator.RoundMoney(payment.AmountPaid ?? draft.Total);
                if (paid < 0 || paid > MaxMoney)
                {
                    throw new BusinessException(MsgInvalidPaid);
                }

                if (paid < draft.Total)
                {
                    throw new BusinessException(MsgPaidTooLow, Money(paid), Money(draft.Total));
                }

                change = paid - draft.Total;
            }
            else
            {
                // No split tender in v1: the whole total goes on one non-cash method.
                paid = draft.Total;
                change = 0m;
            }

            // Friendly stock check first (the inventory ledger re-checks and never goes below zero).
            var sellingWarehouseId = await _warehouses.GetSellingWarehouseIdAsync(cancellationToken);
            var needed = draft.Items
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var ids = needed.Keys.ToList();
            var products = await _db.Products.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var (productId, qty) in needed)
            {
                await EnsureSellingStockAsync(products[productId], qty, cancellationToken, sellingWarehouseId);
            }

            await _inventory.ApplySaleAsync(
                draft.Id,
                sellingWarehouseId,
                needed.Select(x => new SaleStockLineDto { ProductId = x.Key, Quantity = x.Value }).ToList(),
                userId,
                cancellationToken);

            var sequence = (await _db.SalesInvoices.MaxAsync(x => x.InvoiceSequence, cancellationToken) ?? 0) + 1;
            draft.InvoiceSequence = sequence;
            draft.InvoiceNumber = FormatInvoiceNumber(sequence);
            draft.Status = SalesInvoiceStatus.Completed;
            draft.PaymentMethod = method;
            draft.AmountPaid = paid;
            draft.ChangeAmount = change;
            draft.PaymentReference = isCash || string.IsNullOrWhiteSpace(payment.Reference)
                ? null
                : payment.Reference.Trim();
            draft.CompletedAtUtc = Now;
            draft.UpdatedAtUtc = Now;

            if (methodDef.Kind == PaymentMethodKind.Cheque)
            {
                if (draft.CustomerId is null or <= 0)
                {
                    throw new BusinessException(MsgChequeNeedsCustomer);
                }

                if (string.IsNullOrWhiteSpace(payment.ChequeNumber) && string.IsNullOrWhiteSpace(payment.Reference))
                {
                    throw new BusinessException(MsgChequeNumberRequired);
                }

                if (!payment.ChequeDueDate.HasValue)
                {
                    throw new BusinessException(MsgChequeDueRequired);
                }

                var chequeNumber = (payment.ChequeNumber ?? payment.Reference)!.Trim();
                draft.PaymentReference = chequeNumber;
                await _db.SaveChangesAsync(cancellationToken);

                await _cheques.CreateForSaleAsync(new ChequeFormDto
                {
                    Number = chequeNumber,
                    Bank = payment.ChequeBank,
                    DueDate = payment.ChequeDueDate.Value.Date,
                    Amount = draft.Total,
                    Direction = ChequeDirection.In,
                    CustomerId = draft.CustomerId,
                    Notes = draft.InvoiceNumber
                }, draft.Id, userId, cancellationToken);
            }
            else
            {
                // Non-cash settles into the method's default treasury when set (Phase 12).
                if (!isCash && methodDef.DefaultTreasuryId is int settleTreasuryId)
                {
                    await _treasuries.RecordSaleSettlementAsync(
                        settleTreasuryId,
                        draft.Total,
                        draft.Id,
                        userId,
                        note: draft.InvoiceNumber,
                        cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return new CompletedSaleDto
            {
                InvoiceId = draft.Id,
                InvoiceNumber = draft.InvoiceNumber,
                Total = draft.Total,
                PaymentMethod = method,
                AmountPaid = paid,
                ChangeAmount = change
            };
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

    public static string FormatInvoiceNumber(int sequence) =>
        "S-" + sequence.ToString("D6", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- invoices

    public async Task<int?> GetLastInvoiceIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var shiftId = await _shifts.GetActiveShiftIdForUserAsync(userId, cancellationToken);
        if (shiftId is null)
        {
            return null;
        }

        return await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId && x.CashierUserId == userId && x.Status == SalesInvoiceStatus.Completed)
            .OrderByDescending(x => x.InvoiceSequence)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesInvoiceListItemDto>> GetInvoicesAsync(
        SalesInvoiceFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SalesInvoiceFilterDto();
        var query = _db.SalesInvoices.AsNoTracking().Where(x => x.Status == SalesInvoiceStatus.Completed);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.InvoiceNumber != null && x.InvoiceNumber.Contains(term));
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.CompletedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.CompletedAtUtc < filter.ToUtc.Value);
        }

        if (filter.CashierUserId.HasValue)
        {
            query = query.Where(x => x.CashierUserId == filter.CashierUserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(x => x.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.ShiftId.HasValue)
        {
            query = query.Where(x => x.ShiftId == filter.ShiftId.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
        }

        var take = filter.Take <= 0 ? 200 : Math.Min(filter.Take, 1000);
        return await query
            .OrderByDescending(x => x.InvoiceSequence)
            .Take(take)
            .Select(x => new SalesInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber!,
                CompletedAtUtc = x.CompletedAtUtc!.Value,
                ShiftId = x.ShiftId,
                CashierDisplayName = x.CashierUser.DisplayName,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                SaleType = x.SaleType,
                PaymentMethod = x.PaymentMethod!.Value,
                LineCount = x.Items.Count,
                Total = x.Total,
                ReturnedAmount = _db.SalesReturns
                    .Where(r => r.OriginalSalesInvoiceId == x.Id)
                    .Sum(r => (decimal?)r.TotalAmount) ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesInvoiceDetailsDto?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.Id == id && x.Status == SalesInvoiceStatus.Completed)
            .Select(x => new SalesInvoiceDetailsDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber!,
                CompletedAtUtc = x.CompletedAtUtc!.Value,
                ShiftId = x.ShiftId,
                CashierDisplayName = x.CashierUser.DisplayName,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerAddress = x.Customer != null ? x.Customer.Address : null,
                SaleType = x.SaleType,
                Subtotal = x.Subtotal,
                DiscountAmount = x.DiscountAmount,
                DiscountBy = x.DiscountByUser != null ? x.DiscountByUser.DisplayName : null,
                Total = x.Total,
                PaymentMethod = x.PaymentMethod!.Value,
                AmountPaid = x.AmountPaid ?? x.Total,
                ChangeAmount = x.ChangeAmount ?? 0m,
                PaymentReference = x.PaymentReference,
                Lines = x.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new SalesInvoiceLineDto
                    {
                        NameAr = i.ProductNameAr,
                        NameEn = i.ProductNameEn,
                        Barcode = i.Barcode,
                        Unit = i.Unit,
                        Quantity = i.Quantity,
                        CatalogUnitPrice = i.CatalogUnitPrice,
                        UnitPrice = i.UnitPrice,
                        IsPriceOverridden = i.IsPriceOverridden,
                        PriceOverriddenBy = i.PriceOverriddenByUser != null ? i.PriceOverriddenByUser.DisplayName : null,
                        LineTotal = i.LineTotal
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

    // ---------------------------------------------------------------- helpers

    private async Task<int> RequireShiftAsync(int userId, CancellationToken cancellationToken) =>
        await _shifts.GetActiveShiftIdForUserAsync(userId, cancellationToken)
        ?? throw new BusinessException(MsgNotInShift);

    private async Task<SalesInvoice> GetOrCreateDraftAsync(int userId, CancellationToken cancellationToken)
    {
        var shiftId = await RequireShiftAsync(userId, cancellationToken);

        var draft = await _db.SalesInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.CashierUserId == userId && x.Status == SalesInvoiceStatus.Draft, cancellationToken);

        if (draft is not null && draft.ShiftId != shiftId)
        {
            // Left over from an earlier shift (should not happen: close cancels drafts) — start clean.
            draft.Status = SalesInvoiceStatus.Cancelled;
            draft.UpdatedAtUtc = Now;
            await _db.SaveChangesAsync(cancellationToken);
            draft = null;
        }

        if (draft is not null)
        {
            return draft;
        }

        draft = new SalesInvoice
        {
            ShiftId = shiftId,
            CashierUserId = userId,
            Status = SalesInvoiceStatus.Draft,
            SaleType = SaleType.Retail,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        _db.SalesInvoices.Add(draft);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests created the draft at once (UX_SalesInvoices_OneDraftPerCashier) — use the winner.
            _db.Entry(draft).State = EntityState.Detached;
            draft = await _db.SalesInvoices
                .Include(x => x.Items)
                .FirstAsync(x => x.CashierUserId == userId && x.Status == SalesInvoiceStatus.Draft, cancellationToken);
        }

        return draft;
    }

    private async Task<CartDto> SaveCartAsync(SalesInvoice draft, CancellationToken cancellationToken)
    {
        Recalculate(draft);
        draft.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(cancellationToken);
        return await ToCartAsync(draft, cancellationToken);
    }

    private static void Recalculate(SalesInvoice invoice)
    {
        foreach (var line in invoice.Items)
        {
            line.LineTotal = PricingCalculator.RoundMoney(line.Quantity * line.UnitPrice);
        }

        invoice.Subtotal = invoice.Items.Sum(x => x.LineTotal);

        // Removing items can leave a discount bigger than what is left.
        if (invoice.DiscountAmount > invoice.Subtotal)
        {
            invoice.DiscountAmount = invoice.Subtotal;
        }

        if (invoice.DiscountAmount == 0)
        {
            invoice.DiscountByUserId = null;
        }

        invoice.Total = invoice.Subtotal - invoice.DiscountAmount;
    }

    private async Task<CartDto> ToCartAsync(SalesInvoice draft, CancellationToken cancellationToken)
    {
        var heldCount = await _db.SalesInvoices
            .CountAsync(x => x.ShiftId == draft.ShiftId && x.Status == SalesInvoiceStatus.Held, cancellationToken);

        var customer = draft.CustomerId is null
            ? null
            : await _db.Customers.AsNoTracking()
                .Where(x => x.Id == draft.CustomerId.Value)
                .Select(x => new { x.Name, x.Phone })
                .FirstOrDefaultAsync(cancellationToken);

        Recalculate(draft);
        return new CartDto
        {
            InvoiceId = draft.Id,
            ShiftId = draft.ShiftId,
            SaleType = draft.SaleType,
            CustomerId = customer is null ? null : draft.CustomerId,
            CustomerName = customer?.Name,
            CustomerPhone = customer?.Phone,
            Lines = draft.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new CartLineDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    NameAr = x.ProductNameAr,
                    NameEn = x.ProductNameEn,
                    Unit = x.Unit,
                    Quantity = x.Quantity,
                    CatalogUnitPrice = x.CatalogUnitPrice,
                    UnitPrice = x.UnitPrice,
                    IsPriceOverridden = x.IsPriceOverridden,
                    LineTotal = x.LineTotal
                })
                .ToList(),
            ItemCount = draft.Items.Sum(x => x.Quantity),
            Subtotal = draft.Subtotal,
            DiscountAmount = draft.DiscountAmount,
            Total = draft.Total,
            HeldCount = heldCount
        };
    }

    private static SalesInvoiceItem FindLine(SalesInvoice draft, int lineId) =>
        draft.Items.FirstOrDefault(x => x.Id == lineId) ?? throw new BusinessException(MsgLineNotFound);

    private static decimal ValidateQuantity(decimal quantity)
    {
        var qty = Math.Round(quantity, 3, MidpointRounding.AwayFromZero);
        if (qty <= 0 || qty > MaxQuantity)
        {
            throw new BusinessException(MsgInvalidQuantity);
        }

        return qty;
    }

    /// <summary>Zero-stock rule checked per selling warehouse (open decision: default block).</summary>
    private async Task EnsureSellingStockAsync(
        Product product,
        decimal quantity,
        CancellationToken cancellationToken,
        int? sellingWarehouseId = null)
    {
        var warehouseId = sellingWarehouseId
            ?? await _warehouses.GetSellingWarehouseIdAsync(cancellationToken);
        var available = await _inventory.GetWarehouseQuantityAsync(warehouseId, product.Id, cancellationToken);
        if (available <= 0)
        {
            throw new BusinessException(MsgOutOfStock, product.NameEn);
        }

        if (quantity > available)
        {
            throw new BusinessException(MsgNotEnoughStock, Qty(available), product.NameEn);
        }
    }

    private static decimal CatalogPrice(Product product, SaleType saleType) =>
        saleType == SaleType.Wholesale ? product.WholesalePrice : product.RetailPrice;

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
    private static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private async Task<HashSet<int>> GetCategoryWithDescendantsAsync(int categoryId, CancellationToken cancellationToken)
    {
        var all = await _db.Categories.AsNoTracking()
            .Select(x => new { x.Id, x.ParentCategoryId })
            .ToListAsync(cancellationToken);
        var result = new HashSet<int> { categoryId };
        var frontier = new Queue<int>([categoryId]);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var child in all.Where(x => x.ParentCategoryId == current))
            {
                if (result.Add(child.Id))
                {
                    frontier.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync(cancellationToken);
    }
}
