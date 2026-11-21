using Microsoft.Extensions.Localization;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.Printing;

/// <summary>
/// One printed document as data (Phase 24): title, info rows, item table, totals, extras. Built once by
/// <see cref="PrintDocumentBuilder"/> and drawn by both engines — the Razor pages (_PrintDoc80 / _PrintDocA4)
/// and Windows silent printing (<see cref="SilentPrintService"/>) — so a template change reaches both.
/// The template's hidden info / columns / totals are already removed.
/// </summary>
public sealed class PrintedDocument
{
    public required PrintTemplate Template { get; init; }
    public required ReceiptFormat Format { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string? BarcodeSvg { get; init; }
    public List<PrintRow> Info { get; init; } = [];
    public List<PrintColumn> Columns { get; init; } = [];
    public List<string[]> Rows { get; init; } = [];
    public List<PrintRow> Totals { get; init; } = [];
    public string? AmountInWords { get; init; }
    public List<PrintTable> Extra { get; init; } = [];
    public string? Notes { get; init; }

    /// <summary>Printed-at line (shift reports).</summary>
    public string? PrintedAt { get; init; }

    /// <summary>Header/footer words in the receipt language, for silent printing (Phone, Tax no., signatures…).</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    public string Language => Format.Language;
    public StoreSettings Settings => Format.Settings;
}

public sealed record PrintRow(string Key, string Label, string Value, bool Ltr = false, bool Strong = false, bool Grand = false, bool Small = false);

public sealed record PrintColumn(string Key, string Label, bool Numeric = false);

public sealed record PrintTable(string Title, IReadOnlyList<PrintColumn> Columns, IReadOnlyList<string[]> Rows);

/// <summary>Builds <see cref="PrintedDocument"/>s. Labels come from the localizer in the RECEIPT language (store setting).</summary>
public sealed class PrintDocumentBuilder
{
    private readonly IStringLocalizer<SharedResource> _l;

    public PrintDocumentBuilder(IStringLocalizer<SharedResource> localizer)
    {
        _l = localizer;
    }

    private string T(string key) => _l[key].Value;

    private static string Name(PrintTemplate t, ReceiptFormat f, string ar, string en) => t.ItemNames switch
    {
        "ar" => ar,
        "en" => en,
        "both" => string.Equals(ar, en, StringComparison.Ordinal) ? ar : $"{ar} / {en}",
        _ => f.Language == "ar" ? ar : en
    };

    // ------------------------------------------------------------------ sale

    public PrintedDocument Sale(
        SalesInvoiceDetailsDto i,
        ReceiptFormat f,
        PrintTemplate t,
        string paymentName,
        bool paymentIsCash,
        string? barcodeSvg,
        IReadOnlyList<SalesReturnListItemDto>? returns = null)
    {
        using var _ = f.UseCulture();
        var vat = f.Settings.IncludedVat(i.Total);
        var info = new List<PrintRow>
        {
            new("date", T("Date"), f.DateTime(i.CompletedAtUtc), Ltr: true),
            new("cashier", T("Cashier"), i.CashierDisplayName),
            new("customer", T("Customer"), i.CustomerName ?? T("Walk-in customer"))
        };
        if (!string.IsNullOrEmpty(i.CustomerPhone)) info.Add(new("mobile", T("Mobile"), f.Digits(i.CustomerPhone), Ltr: true));
        if (!string.IsNullOrEmpty(i.CustomerAddress)) info.Add(new("address", T("Address"), i.CustomerAddress));
        info.Add(new("shift", T("Shift"), "#" + f.Digits(i.ShiftId.ToString()), Ltr: true));
        info.Add(new("payment", T("Payment"), paymentName));
        if (!string.IsNullOrWhiteSpace(i.PaymentReference)) info.Add(new("reference", T("Reference"), i.PaymentReference, Ltr: true));

        var columns = new List<PrintColumn>
        {
            new("no", "#", true), new("code", T("Barcode")), new("item", T("Item")), new("unit", T("Unit")),
            new("qty", T("Qty"), true), new("catalog", T("Catalog price"), true), new("price", T("Price"), true),
            new("discount", T("Line discount"), true), new("total", T("Total"), true)
        };
        var rows = i.Lines.Select((line, n) => new[]
        {
            f.Digits((n + 1).ToString()), line.Barcode ?? "—", Name(t, f, line.NameAr, line.NameEn), line.Unit,
            f.Qty(line.Quantity), f.Money(line.CatalogUnitPrice), f.Money(line.UnitPrice),
            f.Money(Math.Max(0, (line.CatalogUnitPrice - line.UnitPrice) * line.Quantity)), f.Money(line.LineTotal)
        }).ToList();

        var totals = new List<PrintRow>
        {
            new("count", T("Item count"), f.Qty(i.Lines.Sum(x => x.Quantity))),
            new("subtotal", T("Total before discount"), f.Money(i.Subtotal)),
            new("discount", T("Discount"), f.Money(i.DiscountAmount)),
            new("total", T("Total"), $"{f.Money(i.Total)} {T("EGP")}", Grand: true)
        };
        if (vat > 0) totals.Add(new("vat", string.Format(T("Includes VAT {0}%"), f.Digits(f.Settings.VatPercent.ToString("0.##"))), f.Money(vat), Small: true));
        totals.Add(new("paid", paymentName, f.Money(i.AmountPaid)));
        if (paymentIsCash) totals.Add(new("change", T("Change"), f.Money(i.ChangeAmount)));

        var extra = new List<PrintTable>();
        if (returns is { Count: > 0 })
        {
            extra.Add(new PrintTable(T("Returns against this invoice"),
                [new("return", T("Return")), new("date", T("Date")), new("amount", T("Amount"), true)],
                returns.Select(r => new[] { r.ReturnNumber, f.DateTime(r.CreatedAtUtc), f.Money(r.TotalAmount) }).ToList()));
        }

        var title = $"{T("Sales invoice")} — {(i.SaleType == SaleType.Wholesale ? T("Wholesale") : T("Retail"))}";
        return Finish(t, f, title, i.InvoiceNumber, barcodeSvg, info, columns, rows, totals,
            t.ShowAmountInWords ? AmountInWords.Format(i.Total, f.Language) : null, extra, null);
    }

    // ------------------------------------------------------------------ return

    public PrintedDocument Return(SalesReturnDetailsDto r, ReceiptFormat f, PrintTemplate t, string refundName, string? barcodeSvg)
    {
        using var _ = f.UseCulture();
        var info = new List<PrintRow>
        {
            new("original", T("Original invoice"), r.OriginalInvoiceNumber, Ltr: true),
            new("date", T("Date"), f.DateTime(r.CreatedAtUtc), Ltr: true),
            new("user", T("User"), r.UserDisplayName),
            new("refund", T("Refund method"), refundName)
        };
        if (!string.IsNullOrWhiteSpace(r.Reason)) info.Add(new("reason", T("Reason"), r.Reason));

        var columns = new List<PrintColumn>
        {
            new("no", "#", true), new("item", T("Item")), new("unit", T("Unit")),
            new("qty", T("Qty"), true), new("price", T("Price"), true), new("refund", T("Refund"), true)
        };
        var rows = r.Lines.Select((line, n) => new[]
        {
            f.Digits((n + 1).ToString()), Name(t, f, line.NameAr, line.NameEn), line.Unit,
            f.Qty(line.Quantity), f.Money(line.UnitPrice), f.Money(line.RefundAmount)
        }).ToList();

        var totals = new List<PrintRow>
        {
            new("count", T("Item count"), f.Qty(r.Lines.Sum(x => x.Quantity))),
            new("total", T("Total refund"), $"{f.Money(r.TotalAmount)} {T("EGP")}", Grand: true),
            new("method", refundName, f.Money(r.TotalAmount))
        };

        return Finish(t, f, T("Return slip"), r.ReturnNumber, barcodeSvg, info, columns, rows, totals,
            t.ShowAmountInWords ? AmountInWords.Format(r.TotalAmount, f.Language) : null, [], null);
    }

    // ------------------------------------------------------------------ purchase

    public PrintedDocument Purchase(PurchaseInvoiceDetailDto p, ReceiptFormat f, PrintTemplate t)
    {
        using var _ = f.UseCulture();
        var isAr = f.Language == "ar";
        var info = new List<PrintRow>
        {
            new("date", T("Date"), f.Digits(p.InvoiceDate.ToString("yyyy-MM-dd")), Ltr: true),
            new("status", T("Status"), T(p.Status.ToString())),
            new("supplier", T("Supplier"), isAr ? p.SupplierNameAr : p.SupplierNameEn),
            new("createdBy", T("Created by"), p.CreatedByUsername)
        };
        if (!string.IsNullOrEmpty(p.ConfirmedByUsername)) info.Add(new("confirmedBy", T("Confirmed by"), p.ConfirmedByUsername));

        var columns = new List<PrintColumn>
        {
            new("no", "#", true), new("item", T("Item")), new("unit", T("Unit")),
            new("qty", T("Qty"), true), new("cost", T("Cost"), true), new("total", T("Total"), true)
        };
        var rows = p.Items.Select((line, n) => new[]
        {
            f.Digits((n + 1).ToString()), Name(t, f, line.ProductNameAr, line.ProductNameEn), line.Unit,
            f.Qty(line.Quantity), f.Money(line.UnitCost), f.Money(line.LineTotal)
        }).ToList();

        var totals = new List<PrintRow> { new("total", T("Total"), $"{f.Money(p.SubTotal)} {T("EGP")}", Grand: true) };
        var notes = t.ShowsInfo("notes") ? p.Notes : null;
        return Finish(t, f, T("Purchase invoice"), p.InvoiceNumber, null, info, columns, rows, totals,
            t.ShowAmountInWords ? AmountInWords.Format(p.SubTotal, f.Language) : null, [], notes);
    }

    // ------------------------------------------------------------------ shift report

    public PrintedDocument Shift(ShiftSummaryDto s, ReceiptFormat f, PrintTemplate t, DateTime printedAtUtc)
    {
        using var _ = f.UseCulture();
        var isAr = f.Language == "ar";
        var info = new List<PrintRow>
        {
            new("opened", T("Opened"), f.DateTime(s.OpenedAtUtc), Ltr: true),
            new("openedBy", T("Opened by"), s.OpenedByDisplayName)
        };
        if (s.ClosedAtUtc.HasValue)
        {
            info.Add(new("closed", T("Closed"), f.DateTime(s.ClosedAtUtc.Value), Ltr: true));
            info.Add(new("closedBy", T("Closed by"), s.ClosedByDisplayName ?? ""));
        }

        info.Add(new("participants", T("Participants"), string.Join(T(", "), s.Participants.Select(p => p.DisplayName))));
        info.Add(new("invoices", T("Invoices"), f.Digits(s.InvoiceCount.ToString())));

        var columns = new List<PrintColumn>
        {
            new("cashier", T("Cashier")), new("invoices", T("Invoices"), true), new("cash", T("Cash"), true), new("noncash", T("Non-cash"), true)
        };
        var rows = s.Participants.Select(p => new[]
        {
            p.DisplayName, f.Digits(p.InvoiceCount.ToString()), f.Money(p.CashSales), f.Money(p.NonCashSales)
        }).ToList();

        var result = s.Status == ShiftStatus.Open
            ? T("Shift open")
            : s.Reconciliation switch
            {
                ShiftReconciliation.Matched => T("Matched"),
                ShiftReconciliation.Surplus => T("Surplus"),
                ShiftReconciliation.Shortage => T("Shortage"),
                _ => T("Not reconciled")
            };
        var totals = new List<PrintRow>
        {
            new("methods", T("Cash"), f.Money(s.CashSales)),
            new("methods", T("Visa / Card"), f.Money(s.CardSales)),
            new("methods", T("InstaPay"), f.Money(s.InstaPaySales)),
            new("methods", T("Vodafone Cash"), f.Money(s.VodafoneCashSales)),
            new("totalSales", T("Total sales"), f.Money(s.CashSales + s.NonCashTotal), Strong: true),
            new("opening", T("Opening amount"), f.Money(s.OpeningAmount)),
            new("cashSales", T("Cash sales"), f.Money(s.CashSales)),
            new("refunds", T("Cash refunds"), "−" + f.Money(s.CashRefunds), Ltr: true),
            new("cashouts", string.Format(T("Cash-outs ({0})"), f.Digits(s.CashOutsCount.ToString())), "−" + f.Money(s.CashOutsTotal), Ltr: true),
            new("expected", T("Expected in drawer"), f.Money(s.ExpectedCash), Grand: true)
        };
        if (s.Status == ShiftStatus.Closed)
        {
            totals.Add(new("actual", T("Actual cash"), s.ActualCash.HasValue ? f.Money(s.ActualCash.Value) : T("Not counted")));
            totals.Add(new("difference", T("Difference"), s.CashDifference.HasValue ? f.Money(s.CashDifference.Value) : "—", Ltr: true));
            if (s.HandoverTreasuryId.HasValue)
            {
                totals.Add(new("handover", T("Handed over to"), (isAr ? s.HandoverTreasuryNameAr : s.HandoverTreasuryNameEn) ?? "—"));
                totals.Add(new("handover", T("Handover amount"), s.HandoverAmount.HasValue ? f.Money(s.HandoverAmount.Value) : "—"));
            }
        }

        totals.Add(new("result", T("Result"), result, Strong: true));

        var doc = Finish(t, f, T("Shift report"), "#" + f.Digits(s.Id.ToString()), null, info, columns, rows, totals, null, [], s.CloseNotes);
        return new PrintedDocument
        {
            Template = doc.Template, Format = doc.Format, Title = doc.Title, Number = doc.Number, Info = doc.Info,
            Columns = doc.Columns, Rows = doc.Rows, Totals = doc.Totals, Notes = doc.Notes, Labels = doc.Labels,
            PrintedAt = $"{T("Printed")} {f.DateTime(printedAtUtc)}"
        };
    }

    // ------------------------------------------------------------------ template filter

    private static readonly string[] LabelKeys = ["Phone", "Tax no.", "Commercial register", "Prepared by", "Received by", "Stamp", "Bank details"];

    private PrintedDocument Finish(
        PrintTemplate t,
        ReceiptFormat f,
        string title,
        string number,
        string? barcodeSvg,
        List<PrintRow> info,
        List<PrintColumn> columns,
        List<string[]> rows,
        List<PrintRow> totals,
        string? words,
        List<PrintTable> extra,
        string? notes)
    {
        var keep = columns.Select((c, i) => (c, i)).Where(x => t.ShowsColumn(x.c.Key)).ToList();
        return new PrintedDocument
        {
            Template = t,
            Format = f,
            Title = title,
            Number = number,
            BarcodeSvg = t.ShowBarcode ? barcodeSvg : null,
            Info = info.Where(r => t.ShowsInfo(r.Key)).ToList(),
            Columns = keep.Select(x => x.c).ToList(),
            Rows = rows.Select(r => keep.Select(x => r[x.i]).ToArray()).ToList(),
            Totals = totals.Where(r => t.ShowsTotal(r.Key)).ToList(),
            AmountInWords = words,
            Extra = extra,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            Labels = LabelKeys.ToDictionary(k => k, T)
        };
    }
}
