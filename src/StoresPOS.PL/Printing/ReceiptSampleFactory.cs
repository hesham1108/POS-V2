using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.Printing;

public static class ReceiptSampleFactory
{
    public static SalesInvoiceDetailsDto CreateSampleInvoice(StoreSettings settings)
    {
        var lines = new List<SalesInvoiceLineDto>
        {
            new()
            {
                NameAr = "منتج تجريبي ١",
                NameEn = "Sample product 1",
                Unit = "pcs",
                Quantity = 2,
                CatalogUnitPrice = 25m,
                UnitPrice = 25m,
                LineTotal = 50m
            },
            new()
            {
                NameAr = "منتج تجريبي ٢",
                NameEn = "Sample product 2",
                Unit = "pcs",
                Quantity = 1,
                CatalogUnitPrice = 40m,
                UnitPrice = 40m,
                LineTotal = 40m
            }
        };

        return new SalesInvoiceDetailsDto
        {
            Id = 0,
            InvoiceNumber = (settings.InvoiceNumberPrefix ?? "S-") + "PREVIEW",
            CompletedAtUtc = DateTime.UtcNow,
            ShiftId = 1,
            CashierDisplayName = "Preview",
            CustomerName = null,
            SaleType = SaleType.Retail,
            Subtotal = 90m,
            DiscountAmount = 0m,
            Total = 90m,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = 100m,
            ChangeAmount = 10m,
            Lines = lines
        };
    }

    /// <summary>Sample return slip for the template designer preview (Phase 24).</summary>
    public static SalesReturnDetailsDto CreateSampleReturn(StoreSettings settings) => new()
    {
        Id = 0,
        ReturnNumber = (settings.ReturnNumberPrefix ?? "R-") + "PREVIEW",
        OriginalInvoiceNumber = (settings.InvoiceNumberPrefix ?? "S-") + "PREVIEW",
        CreatedAtUtc = DateTime.UtcNow,
        UserDisplayName = "Preview",
        Reason = "Damaged",
        RefundMethod = PaymentMethod.Cash,
        TotalAmount = 25m,
        Lines = [new SalesReturnLineDto { NameAr = "منتج تجريبي ١", NameEn = "Sample product 1", Unit = "pcs", Quantity = 1, UnitPrice = 25m, RefundAmount = 25m }]
    };

    public static PurchaseInvoiceDetailDto CreateSamplePurchase() => new()
    {
        Id = 0,
        InvoiceNumber = "P-PREVIEW",
        SupplierNameAr = "مورد تجريبي",
        SupplierNameEn = "Sample supplier",
        InvoiceDate = DateTime.Today,
        Status = PurchaseInvoiceStatus.Confirmed,
        CreatedByUsername = "admin",
        ConfirmedByUsername = "admin",
        SubTotal = 650m,
        Items =
        [
            new PurchaseInvoiceItemDto { ProductNameAr = "منتج تجريبي ١", ProductNameEn = "Sample product 1", Unit = "pcs", Quantity = 10, UnitCost = 50m, LineTotal = 500m },
            new PurchaseInvoiceItemDto { ProductNameAr = "منتج تجريبي ٢", ProductNameEn = "Sample product 2", Unit = "pcs", Quantity = 5, UnitCost = 30m, LineTotal = 150m }
        ]
    };

    public static ShiftSummaryDto CreateSampleShift() => new()
    {
        Id = 0,
        Status = ShiftStatus.Closed,
        OpenedAtUtc = DateTime.UtcNow.AddHours(-8),
        OpenedByDisplayName = "Preview",
        OpeningAmount = 500m,
        CashSales = 2400m,
        CardSales = 800m,
        InstaPaySales = 150m,
        VodafoneCashSales = 50m,
        CashRefunds = 25m,
        CashOutsCount = 1,
        CashOutsTotal = 75m,
        ExpectedCash = 2800m,
        ActualCash = 2790m,
        CashDifference = -10m,
        Reconciliation = ShiftReconciliation.Shortage,
        InvoiceCount = 42,
        ClosedAtUtc = DateTime.UtcNow,
        ClosedByDisplayName = "Preview",
        Participants =
        [
            new ShiftParticipantDto { DisplayName = "Cashier 1", InvoiceCount = 25, CashSales = 1500m, NonCashSales = 600m },
            new ShiftParticipantDto { DisplayName = "Cashier 2", InvoiceCount = 17, CashSales = 900m, NonCashSales = 400m }
        ]
    };
}
