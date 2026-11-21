using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

/// <summary>
/// Reports load the rows of the requested range once and aggregate in memory: one store, bounded ranges,
/// and the same code runs on SQL Server and the in-memory test provider.
/// </summary>
public partial class ReportService : IReportService
{
    private const int TopProductsCount = 10;

    private readonly ApplicationDbContext _db;
    private readonly IShiftService _shifts;
    private readonly ICashOutService _cashOuts;
    private readonly TimeProvider _time;

    public ReportService(ApplicationDbContext db, IShiftService shifts, ICashOutService cashOuts, TimeProvider time)
    {
        _db = db;
        _shifts = shifts;
        _cashOuts = cashOuts;
        _time = time;
    }

    // ------------------------------------------------------------------ sales

    public async Task<SalesReportDto> GetSalesAsync(SalesReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var invoices = _db.SalesInvoices.AsNoTracking().Where(x => x.Status == SalesInvoiceStatus.Completed);
        if (filter.FromUtc.HasValue)
        {
            invoices = invoices.Where(x => x.CompletedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            invoices = invoices.Where(x => x.CompletedAtUtc < filter.ToUtc.Value);
        }

        if (filter.CashierUserId.HasValue)
        {
            invoices = invoices.Where(x => x.CashierUserId == filter.CashierUserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            invoices = invoices.Where(x => x.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.SaleType.HasValue)
        {
            invoices = invoices.Where(x => x.SaleType == filter.SaleType.Value);
        }

        if (filter.CustomerGroup is { } group)
        {
            if (group == SalesReportFilterDto.WalkInGroup)
            {
                invoices = invoices.Where(x => x.CustomerId == null);
            }
            else
            {
                var type = (CustomerType)group;
                invoices = invoices.Where(x => x.Customer != null && x.Customer.CustomerType == type);
            }
        }

        HashSet<int>? categoryIds = filter.CategoryId.HasValue
            ? await GetCategoryWithDescendantsAsync(filter.CategoryId.Value, cancellationToken)
            : null;

        var lines = await _db.SalesInvoiceItems.AsNoTracking()
            .Where(i => invoices.Any(inv => inv.Id == i.SalesInvoiceId))
            .Select(i => new
            {
                i.SalesInvoiceId,
                i.ProductId,
                i.ProductNameEn,
                i.ProductNameAr,
                i.Unit,
                i.Quantity,
                i.LineTotal,
                i.UnitCost,
                CategoryId = i.Product.CategoryId,
                InvoiceSubtotal = i.SalesInvoice.Subtotal,
                InvoiceTotal = i.SalesInvoice.Total,
                CompletedAtUtc = i.SalesInvoice.CompletedAtUtc!.Value,
                Method = i.SalesInvoice.PaymentMethod!.Value,
                i.SalesInvoice.SaleType,
                CashierId = i.SalesInvoice.CashierUserId,
                CashierName = i.SalesInvoice.CashierUser.DisplayName,
                i.SalesInvoice.CustomerId,
                CustomerName = i.SalesInvoice.Customer != null ? i.SalesInvoice.Customer.Name : null,
                CustomerType = i.SalesInvoice.Customer != null ? (int?)i.SalesInvoice.Customer.CustomerType : null
            })
            .ToListAsync(cancellationToken);

        if (categoryIds is not null)
        {
            lines = lines.Where(x => x.CategoryId.HasValue && categoryIds.Contains(x.CategoryId.Value)).ToList();
        }

        // Each line carries its share of the invoice discount, so every breakdown adds up to net sales.
        var rows = lines.Select(x => new
        {
            x.SalesInvoiceId,
            x.ProductId,
            x.ProductNameEn,
            x.ProductNameAr,
            x.Unit,
            x.Quantity,
            Gross = x.LineTotal,
            Net = x.InvoiceSubtotal == 0 ? 0m : PricingCalculator.RoundMoney(x.LineTotal * x.InvoiceTotal / x.InvoiceSubtotal),
            Cost = PricingCalculator.RoundMoney(x.UnitCost * x.Quantity),
            x.CompletedAtUtc,
            x.Method,
            x.SaleType,
            x.CashierId,
            x.CashierName,
            x.CustomerId,
            x.CustomerName,
            CustomerGroup = x.CustomerType ?? SalesReportFilterDto.WalkInGroup
        }).ToList();

        // Returns made in the range against invoices that match the same filters.
        // Dated by when the refund happened (a return today against last week's sale counts today).
        var returnsQuery = _db.SalesReturnItems.AsNoTracking();
        if (filter.FromUtc.HasValue)
        {
            returnsQuery = returnsQuery.Where(r => r.SalesReturn.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            returnsQuery = returnsQuery.Where(r => r.SalesReturn.CreatedAtUtc < filter.ToUtc.Value);
        }

        if (filter.CashierUserId.HasValue)
        {
            returnsQuery = returnsQuery.Where(r => r.SalesReturn.OriginalSalesInvoice.CashierUserId == filter.CashierUserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            returnsQuery = returnsQuery.Where(r => r.SalesReturn.OriginalSalesInvoice.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.SaleType.HasValue)
        {
            returnsQuery = returnsQuery.Where(r => r.SalesReturn.OriginalSalesInvoice.SaleType == filter.SaleType.Value);
        }

        if (filter.CustomerGroup is { } returnGroup)
        {
            if (returnGroup == SalesReportFilterDto.WalkInGroup)
            {
                returnsQuery = returnsQuery.Where(r => r.SalesReturn.OriginalSalesInvoice.CustomerId == null);
            }
            else
            {
                var type = (CustomerType)returnGroup;
                returnsQuery = returnsQuery.Where(r => r.SalesReturn.OriginalSalesInvoice.Customer != null
                    && r.SalesReturn.OriginalSalesInvoice.Customer.CustomerType == type);
            }
        }

        var returnLines = await returnsQuery
            .Select(r => new
            {
                r.SalesReturnId,
                r.ProductId,
                r.RefundAmount,
                Cost = r.SalesInvoiceItem.UnitCost * r.Quantity,
                r.Product.CategoryId
            })
            .ToListAsync(cancellationToken);

        if (categoryIds is not null)
        {
            returnLines = returnLines.Where(x => x.CategoryId.HasValue && categoryIds.Contains(x.CategoryId.Value)).ToList();
        }

        var zone = _time.LocalTimeZone;
        var gross = rows.Sum(x => x.Gross);
        var net = rows.Sum(x => x.Net);

        return new SalesReportDto
        {
            InvoiceCount = rows.Select(x => x.SalesInvoiceId).Distinct().Count(),
            GrossSales = gross,
            Discounts = gross - net,
            NetSales = net,
            ReturnsCount = returnLines.Select(x => x.SalesReturnId).Distinct().Count(),
            ReturnsAmount = returnLines.Sum(x => x.RefundAmount),
            Cost = rows.Sum(x => x.Cost) - PricingCalculator.RoundMoney(returnLines.Sum(x => x.Cost)),
            ByMethod = await BuildByMethodAsync(
                rows.GroupBy(x => x.Method)
                    .Select(g => (g.Key, Count: g.Select(x => x.SalesInvoiceId).Distinct().Count(), Amount: g.Sum(x => x.Net)))
                    .ToList(),
                cancellationToken),
            ByCashier = rows
                .GroupBy(x => new { x.CashierId, x.CashierName })
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key.CashierId,
                    Name = g.Key.CashierName,
                    Count = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    Amount = g.Sum(x => x.Net)
                })
                .OrderByDescending(x => x.Amount)
                .ToList(),
            BySaleType = rows
                .GroupBy(x => x.SaleType)
                .Select(g => new NamedAmountDto
                {
                    Id = (int)g.Key,
                    Name = g.Key.ToString(),
                    Count = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    Amount = g.Sum(x => x.Net)
                })
                .OrderBy(x => x.Id)
                .ToList(),
            ByCustomerType = rows
                .GroupBy(x => x.CustomerGroup)
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key,
                    Name = g.Key == SalesReportFilterDto.WalkInGroup ? "WalkIn" : ((CustomerType)g.Key).ToString(),
                    Count = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    Amount = g.Sum(x => x.Net)
                })
                .OrderBy(x => x.Id == SalesReportFilterDto.WalkInGroup ? 99 : x.Id)
                .ToList(),
            TopCustomers = rows
                .Where(x => x.CustomerId.HasValue)
                .GroupBy(x => new { Id = x.CustomerId!.Value, x.CustomerName })
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.CustomerName ?? string.Empty,
                    Count = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    Amount = g.Sum(x => x.Net)
                })
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.Name)
                .Take(TopProductsCount)
                .ToList(),
            ByDay = rows
                .GroupBy(x => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.CompletedAtUtc, DateTimeKind.Utc), zone).Date)
                .Select(g => new DailyAmountDto
                {
                    Date = g.Key,
                    Count = g.Select(x => x.SalesInvoiceId).Distinct().Count(),
                    Amount = g.Sum(x => x.Net)
                })
                .OrderBy(x => x.Date)
                .ToList(),
            TopProducts = rows
                .GroupBy(x => x.ProductId)
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key,
                    NameEn = g.First().ProductNameEn,
                    NameAr = g.First().ProductNameAr,
                    Unit = g.First().Unit,
                    Quantity = g.Sum(x => x.Quantity),
                    Amount = g.Sum(x => x.Net),
                    Profit = g.Sum(x => x.Net - x.Cost)
                })
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.NameEn)
                .Take(TopProductsCount)
                .ToList()
        };
    }

    // ------------------------------------------------------------------ purchases

    public async Task<PurchasesReportDto> GetPurchasesAsync(PurchasesReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var invoices = _db.PurchaseInvoices.AsNoTracking().Where(x => x.Status == PurchaseInvoiceStatus.Confirmed);

        // Purchases are reported by their invoice date (the supplier's date), not the confirm time.
        if (filter.FromUtc.HasValue)
        {
            invoices = invoices.Where(x => x.InvoiceDate >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            invoices = invoices.Where(x => x.InvoiceDate < filter.ToUtc.Value);
        }

        if (filter.SupplierId.HasValue)
        {
            invoices = invoices.Where(x => x.SupplierId == filter.SupplierId.Value);
        }

        var list = await invoices
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new PurchasesReportInvoiceDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                InvoiceDate = x.InvoiceDate,
                SupplierName = x.Supplier.NameEn,
                LineCount = x.Items.Count,
                Total = x.SubTotal
            })
            .ToListAsync(cancellationToken);

        var bySupplier = await invoices
            .Select(x => new { x.SupplierId, x.Supplier.NameEn, x.Supplier.NameAr, x.SubTotal })
            .ToListAsync(cancellationToken);

        var items = await _db.PurchaseInvoiceItems.AsNoTracking()
            .Where(i => invoices.Any(inv => inv.Id == i.PurchaseInvoiceId))
            .Select(i => new { i.ProductId, i.ProductNameEn, i.ProductNameAr, i.Quantity, i.LineTotal, i.PurchaseInvoiceId })
            .ToListAsync(cancellationToken);

        return new PurchasesReportDto
        {
            InvoiceCount = list.Count,
            Total = list.Sum(x => x.Total),
            Invoices = list,
            BySupplier = bySupplier
                .GroupBy(x => x.SupplierId)
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key,
                    Name = g.First().NameEn,
                    NameAr = g.First().NameAr,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.Amount)
                .ToList(),
            ByProduct = items
                .GroupBy(x => x.ProductId)
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key,
                    Name = g.First().ProductNameEn,
                    NameAr = g.First().ProductNameAr,
                    Count = g.Select(x => x.PurchaseInvoiceId).Distinct().Count(),
                    Quantity = g.Sum(x => x.Quantity),
                    Amount = g.Sum(x => x.LineTotal)
                })
                .OrderByDescending(x => x.Amount)
                .ToList()
        };
    }

    // ------------------------------------------------------------------ inventory

    public async Task<InventoryReportDto> GetInventoryAsync(InventoryReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var products = _db.Products.AsNoTracking().Where(x => x.IsActive);
        if (filter.CategoryId.HasValue)
        {
            var ids = await GetCategoryWithDescendantsAsync(filter.CategoryId.Value, cancellationToken);
            products = products.Where(x => x.CategoryId.HasValue && ids.Contains(x.CategoryId.Value));
        }

        List<InventoryReportRowDto> rows;
        if (filter.WarehouseId.HasValue)
        {
            var warehouseId = filter.WarehouseId.Value;
            var q =
                from p in products
                join ws in _db.WarehouseStocks.AsNoTracking().Where(x => x.WarehouseId == warehouseId)
                    on p.Id equals ws.ProductId into stocks
                from ws in stocks.DefaultIfEmpty()
                select new { Product = p, Qty = ws != null ? ws.Quantity : 0m };

            if (filter.LowStockOnly)
            {
                q = q.Where(x => x.Qty <= x.Product.LowStockThreshold);
            }

            rows = await q
                .OrderBy(x => x.Product.NameEn)
                .Select(x => new InventoryReportRowDto
                {
                    ProductId = x.Product.Id,
                    NameEn = x.Product.NameEn,
                    NameAr = x.Product.NameAr,
                    CategoryNameEn = x.Product.Category != null ? x.Product.Category.NameEn : null,
                    CategoryNameAr = x.Product.Category != null ? x.Product.Category.NameAr : null,
                    Unit = x.Product.Unit,
                    StockQuantity = x.Qty,
                    LowStockThreshold = x.Product.LowStockThreshold,
                    UnitCost = x.Product.PurchasePrice,
                    RetailPrice = x.Product.RetailPrice
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            if (filter.LowStockOnly)
            {
                products = products.Where(x => x.StockQuantity <= x.LowStockThreshold);
            }

            rows = await products
                .OrderBy(x => x.NameEn)
                .Select(x => new InventoryReportRowDto
                {
                    ProductId = x.Id,
                    NameEn = x.NameEn,
                    NameAr = x.NameAr,
                    CategoryNameEn = x.Category != null ? x.Category.NameEn : null,
                    CategoryNameAr = x.Category != null ? x.Category.NameAr : null,
                    Unit = x.Unit,
                    StockQuantity = x.StockQuantity,
                    LowStockThreshold = x.LowStockThreshold,
                    UnitCost = x.PurchasePrice,
                    RetailPrice = x.RetailPrice
                })
                .ToListAsync(cancellationToken);
        }

        foreach (var row in rows)
        {
            // Negative stock is not possible (inventory blocks it); guard anyway so value never goes below zero.
            var qty = Math.Max(row.StockQuantity, 0m);
            row.CostValue = PricingCalculator.RoundMoney(qty * row.UnitCost);
            row.RetailValue = PricingCalculator.RoundMoney(qty * row.RetailPrice);
        }

        var movements = _db.StockMovements.AsNoTracking().Where(m => products.Any(p => p.Id == m.ProductId));
        if (filter.WarehouseId.HasValue)
        {
            movements = movements.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            movements = movements.Where(x => x.CreatedAtUtc < filter.ToUtc.Value);
        }

        var moves = await movements.Select(x => new { x.MovementType, x.QuantityChange }).ToListAsync(cancellationToken);

        return new InventoryReportDto
        {
            Rows = rows,
            Movements = moves
                .GroupBy(x => x.MovementType)
                .Select(g => new MovementSummaryDto
                {
                    Type = g.Key,
                    Count = g.Count(),
                    QuantityIn = g.Where(x => x.QuantityChange > 0).Sum(x => x.QuantityChange),
                    QuantityOut = -g.Where(x => x.QuantityChange < 0).Sum(x => x.QuantityChange)
                })
                .OrderBy(x => x.Type)
                .ToList()
        };
    }

    // ------------------------------------------------------------------ shifts

    public async Task<ShiftsReportDto> GetShiftsAsync(ShiftHistoryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var shifts = await _shifts.GetHistoryAsync(filter, cancellationToken);
        var ids = shifts.Select(x => x.Id).ToList();

        var sales = await _db.SalesInvoices.AsNoTracking()
            .Where(x => ids.Contains(x.ShiftId) && x.Status == SalesInvoiceStatus.Completed)
            .Select(x => new { x.CashierUserId, x.CashierUser.DisplayName, x.Total })
            .ToListAsync(cancellationToken);

        return new ShiftsReportDto
        {
            Shifts = shifts,
            ByCashier = sales
                .GroupBy(x => x.CashierUserId)
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key,
                    Name = g.First().DisplayName,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Total)
                })
                .OrderByDescending(x => x.Amount)
                .ToList()
        };
    }

    // ------------------------------------------------------------------ cash-outs

    public async Task<CashOutsReportDto> GetCashOutsAsync(CashOutsReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var items = await _cashOuts.GetListAsync(new CashOutFilterDto
        {
            FromUtc = filter.FromUtc,
            ToUtc = filter.ToUtc,
            UserId = filter.UserId,
            Take = 5000
        }, cancellationToken);

        return new CashOutsReportDto
        {
            Items = items,
            ByUser = items
                .GroupBy(x => x.UserId)
                .Select(g => new NamedAmountDto
                {
                    Id = g.Key,
                    Name = g.First().UserDisplayName,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList(),
            // Reasons are free text: group case-insensitively on the trimmed text.
            ByReason = items
                .GroupBy(x => x.Reason.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new NamedAmountDto
                {
                    Name = g.First().Reason.Trim(),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList()
        };
    }

    public async Task<IReadOnlyList<NamedAmountDto>> GetUserOptionsAsync(CancellationToken cancellationToken = default) =>
        await _db.Users.AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new NamedAmountDto { Id = x.Id, Name = x.DisplayName })
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------ helpers

    private async Task<IReadOnlyList<PaymentMethodAmountDto>> BuildByMethodAsync(
        IReadOnlyList<(PaymentMethod Method, int Count, decimal Amount)> groups,
        CancellationToken cancellationToken)
    {
        var lookup = await _db.PaymentMethods.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return groups
            .Select(g =>
            {
                lookup.TryGetValue((byte)g.Method, out var def);
                return new PaymentMethodAmountDto
                {
                    Method = g.Method,
                    Count = g.Count,
                    Amount = g.Amount,
                    NameEn = def?.NameEn ?? g.Method.ToString(),
                    NameAr = def?.NameAr ?? g.Method.ToString(),
                    IsCash = def?.Kind == PaymentMethodKind.Cash || g.Method == PaymentMethod.Cash
                };
            })
            .OrderBy(x => x.Method)
            .ToList();
    }

    private async Task<HashSet<int>> GetCategoryWithDescendantsAsync(int categoryId, CancellationToken cancellationToken)
    {
        var all = await _db.Categories.AsNoTracking()
            .Select(x => new { x.Id, x.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var result = new HashSet<int> { categoryId };
        var frontier = new Queue<int>();
        frontier.Enqueue(categoryId);
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
}
