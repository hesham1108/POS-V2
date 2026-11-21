using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Services;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Entities;

namespace StoresPOS.PL.Printing;

/// <summary>
/// Windows silent printing (no browser dialog). Phase 24: prints the same <see cref="PrintedDocument"/> the
/// browser page shows (same template: section order, switches, texts), drawn as text lines with System.Drawing.
/// </summary>
public sealed class SilentPrintService : ISilentPrintService
{
    private readonly ISalesService _sales;
    private readonly IWindowsPrinterService _printers;
    private readonly IOptionsMonitor<StoreSettings> _store;
    private readonly IPrintTemplateService _templates;
    private readonly IPaymentMethodService _methods;
    private readonly IStringLocalizer<SharedResource> _l;

    public SilentPrintService(
        ISalesService sales,
        IWindowsPrinterService printers,
        IOptionsMonitor<StoreSettings> store,
        IPrintTemplateService templates,
        IPaymentMethodService methods,
        IStringLocalizer<SharedResource> localizer)
    {
        _sales = sales;
        _printers = printers;
        _store = store;
        _templates = templates;
        _methods = methods;
        _l = localizer;
    }

    public async Task<SilentPrintResult> PrintSaleAsync(
        int invoiceId,
        string? format,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _sales.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return SilentPrintResult.Fail("Invoice not found.", browserFallback: false);
        }

        var settings = _store.CurrentValue;
        var resolved = ResolveFormat(settings, PrintDocumentType.Sale, format);
        var copies = settings.PrintSettingsFor(PrintDocumentType.Sale).Copies;
        var receipt = new ReceiptFormat(settings);
        var method = await _methods.GetAsync((byte)invoice.PaymentMethod, cancellationToken);
        var doc = new PrintDocumentBuilder(_l).Sale(
            invoice,
            receipt,
            _templates.Get(PrintTemplateCatalog.Sale, resolved, settings),
            method?.NameFor(receipt.Language) ?? invoice.PaymentMethod.ToString(),
            method?.IsCash == true || invoice.PaymentMethod == PaymentMethod.Cash,
            barcodeSvg: null);
        return Print(doc, settings, resolved, copies, printerOverride: null);
    }

    public SilentPrintResult PrintSample(string? format, string? printerName = null)
    {
        var settings = _store.CurrentValue;
        var resolved = ResolveFormat(settings, PrintDocumentType.Sale, format);
        var receipt = new ReceiptFormat(settings);
        string cash;
        using (receipt.UseCulture())
        {
            cash = _l["Cash"].Value;
        }

        var doc = new PrintDocumentBuilder(_l).Sale(
            ReceiptSampleFactory.CreateSampleInvoice(settings),
            receipt,
            _templates.Get(PrintTemplateCatalog.Sale, resolved, settings),
            cash,
            paymentIsCash: true,
            barcodeSvg: null);
        return Print(doc, settings, resolved, copies: 1, printerOverride: printerName);
    }

    /// <summary>Requested format ("80mm"/"A4"), else the document's default format.</summary>
    public static string ResolveFormat(StoreSettings settings, PrintDocumentType document, string? requested) =>
        PrintFormats.IsKnown(requested) ? PrintFormats.Normalize(requested) : settings.FormatFor(document);

    /// <summary>
    /// Printer for a paper format (Phase 19): the format's printer (thermal / A4) → the store default
    /// printer → the Windows default printer.
    /// </summary>
    public static string? ResolvePrinterName(StoreSettings settings, string format, string? windowsDefault) =>
        settings.PrinterFor(format) ?? (string.IsNullOrWhiteSpace(windowsDefault) ? null : windowsDefault);

    private SilentPrintResult Print(
        PrintedDocument doc,
        StoreSettings settings,
        string format,
        int copies,
        string? printerOverride)
    {
        if (!_printers.IsSupported)
        {
            return SilentPrintResult.Fail("Silent printing requires Windows.");
        }

        var printer = string.IsNullOrWhiteSpace(printerOverride)
            ? ResolvePrinterName(settings, format, _printers.GetWindowsDefault())
            : printerOverride.Trim();
        if (string.IsNullOrWhiteSpace(printer))
        {
            return SilentPrintResult.Fail("No printer selected. Choose a default printer in Settings → Printing.");
        }

        var installed = _printers.ListPrinters();
        if (!installed.Any(p => string.Equals(p, printer, StringComparison.OrdinalIgnoreCase)))
        {
            // The cached list may be older than a newly added printer: ask Windows once more.
            _printers.Invalidate();
            installed = _printers.ListPrinters();
        }

        if (!installed.Any(p => string.Equals(p, printer, StringComparison.OrdinalIgnoreCase)))
        {
            return SilentPrintResult.Fail($"Printer '{printer}' was not found. Refresh the printer list in Settings.");
        }

        try
        {
            var count = Math.Clamp(copies, 1, 5);
            var lines = BuildLines(doc);
            if (format == PrintFormats.A4)
            {
                PrintA4(lines, doc, printer, count);
            }
            else
            {
                Print80(lines, doc, settings, printer, count);
            }

            return SilentPrintResult.Ok(printer, $"Printed silently to {printer}.");
        }
        catch (Exception ex)
        {
            return SilentPrintResult.Fail($"Print failed: {ex.Message}");
        }
    }

    /// <summary>Line height factor for the template's density.</summary>
    private static float Density(PrintTemplate t) => t.Density switch { "compact" => 0.85f, "relaxed" => 1.25f, _ => 1f };

    [SupportedOSPlatform("windows")]
    private static void Print80(List<(string Text, bool Bold)> lines, PrintedDocument d, StoreSettings settings, string printer, int copies)
    {
        var isAr = d.Language == "ar";
        var scale = (float)d.Template.FontScale;
        var widthHundredths = settings.PaperWidthMm <= 58 ? 227 : 315; // ~58mm / 80mm
        var lineHeight = (int)Math.Ceiling(14 * scale * Density(d.Template));
        var page = 0;

        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.PrinterSettings.Copies = (short)copies;
        doc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);
        doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt", widthHundredths, Math.Max(600, lines.Count * lineHeight + 80));

        doc.PrintPage += (_, e) =>
        {
            using var font = new Font("Segoe UI", 8f * scale);
            using var bold = new Font("Segoe UI", 9f * scale, FontStyle.Bold);
            var g = e.Graphics!;
            var left = e.MarginBounds.Left;
            var width = e.MarginBounds.Width;
            var format = new StringFormat(isAr ? StringFormatFlags.DirectionRightToLeft : StringFormatFlags.NoClip)
            {
                Alignment = isAr ? StringAlignment.Far : StringAlignment.Near
            };

            var top = e.MarginBounds.Top;
            for (; page < lines.Count; page++)
            {
                if (top + lineHeight > e.MarginBounds.Bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                var (text, strong) = lines[page];
                g.DrawString(text, strong ? bold : font, Brushes.Black, new RectangleF(left, top, width, lineHeight + 2), format);
                top += lineHeight;
            }

            e.HasMorePages = false;
        };

        doc.Print();
    }

    [SupportedOSPlatform("windows")]
    private static void PrintA4(List<(string Text, bool Bold)> lines, PrintedDocument d, string printer, int copies)
    {
        var isAr = d.Language == "ar";
        var t = d.Template;
        var scale = (float)t.FontScale;
        var margin = (int)Math.Round(t.MarginMm / 25.4 * 100); // hundredths of an inch
        var lineHeight = (int)Math.Ceiling(16 * scale * Density(t));
        var index = 0;

        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.PrinterSettings.Copies = (short)copies;
        doc.DefaultPageSettings.Landscape = t.Landscape;
        doc.DefaultPageSettings.Margins = new Margins(margin, margin, margin, margin);

        doc.PrintPage += (_, e) =>
        {
            using var font = new Font("Segoe UI", 9f * scale);
            using var bold = new Font("Segoe UI", 9.5f * scale, FontStyle.Bold);
            var g = e.Graphics!;
            var left = e.MarginBounds.Left;
            var width = e.MarginBounds.Width;
            var format = new StringFormat(isAr ? StringFormatFlags.DirectionRightToLeft : StringFormatFlags.NoClip)
            {
                Alignment = isAr ? StringAlignment.Far : StringAlignment.Near
            };

            var y = e.MarginBounds.Top;
            for (; index < lines.Count; index++)
            {
                if (y + lineHeight > e.MarginBounds.Bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                var (text, strong) = lines[index];
                g.DrawString(text, strong ? bold : font, Brushes.Black, new RectangleF(left, y, width, lineHeight + 2), format);
                y += lineHeight;
            }

            e.HasMorePages = false;
        };

        doc.Print();
    }

    /// <summary>
    /// The document as text lines, sections in the template's order with the same switches as the page
    /// (logo and barcode images are left out of silent printing).
    /// </summary>
    public static List<(string Text, bool Bold)> BuildLines(PrintedDocument d)
    {
        var t = d.Template;
        var s = d.Settings;
        var lang = d.Language;
        var rule = new string('-', t.IsA4 ? 90 : 32);
        var list = new List<(string, bool)>();
        void Rule()
        {
            if (list.Count > 0 && list[^1].Item1 != rule)
            {
                list.Add((rule, false));
            }
        }

        foreach (var section in t.SectionOrder.Where(t.Shows))
        {
            switch (section)
            {
                case "header":
                    if (t.ShowStoreName) list.Add((s.NameFor(lang), true));
                    if (t.ShowAddress && !string.IsNullOrWhiteSpace(s.AddressFor(lang))) list.Add((s.AddressFor(lang)!, false));
                    if (t.ShowPhone && !string.IsNullOrWhiteSpace(s.Phone)) list.Add(($"{Label(d, "Phone")} {s.Phone}", false));
                    if (t.ShowTaxNumber && !string.IsNullOrWhiteSpace(s.TaxNumber)) list.Add(($"{Label(d, "Tax no.")} {s.TaxNumber}", false));
                    if (t.ShowCommercialRegister && !string.IsNullOrWhiteSpace(s.CommercialRegister)) list.Add(($"{Label(d, "Commercial register")} {s.CommercialRegister}", false));
                    if (!string.IsNullOrWhiteSpace(t.HeaderLineFor(lang))) list.Add((t.HeaderLineFor(lang)!, true));
                    if (!t.Shows("info")) list.Add(($"{d.Title} {d.Number}", true));
                    break;

                case "info":
                    Rule();
                    list.Add((d.Title, true));
                    list.Add((d.Number, true));
                    foreach (var r in d.Info) list.Add(($"{r.Label}: {r.Value}", false));
                    break;

                case "items":
                    if (d.Rows.Count == 0) break;
                    Rule();
                    if (t.IsA4)
                    {
                        list.Add((string.Join(" | ", d.Columns.Select(c => c.Label)), true));
                        foreach (var row in d.Rows) list.Add((string.Join(" | ", row), false));
                    }
                    else
                    {
                        foreach (var row in d.Rows)
                        {
                            var text = d.Columns.Select((c, i) => (c, v: row[i])).Where(x => !x.c.Numeric).Select(x => x.v).ToList();
                            var nums = d.Columns.Select((c, i) => (c, v: row[i])).Where(x => x.c.Numeric).Select(x => x.v).ToList();
                            if (text.Count > 0) list.Add((string.Join(" · ", text), false));
                            if (nums.Count > 0) list.Add(("  " + string.Join("  ", nums), false));
                        }
                    }

                    break;

                case "totals":
                    Rule();
                    foreach (var r in d.Totals) list.Add(($"{r.Label}: {r.Value}", r.Grand || r.Strong));
                    if (!string.IsNullOrWhiteSpace(d.AmountInWords)) list.Add((d.AmountInWords!, false));
                    foreach (var x in d.Extra)
                    {
                        list.Add((x.Title, true));
                        foreach (var row in x.Rows) list.Add((string.Join(" | ", row), false));
                    }

                    if (d.Notes is not null) list.Add((d.Notes, false));
                    break;

                case "footer":
                    Rule();
                    if (t.ShowFooterText && !string.IsNullOrWhiteSpace(s.FooterFor(lang))) list.Add((s.FooterFor(lang)!, false));
                    if (!string.IsNullOrWhiteSpace(t.ReturnPolicyFor(lang))) list.Add((t.ReturnPolicyFor(lang)!, false));
                    if (!string.IsNullOrWhiteSpace(t.BankDetailsFor(lang))) list.Add((t.BankDetailsFor(lang)!, false));
                    if (t.ShowSignatures && t.IsA4)
                    {
                        list.Add((string.Empty, false));
                        list.Add(($"{Label(d, "Prepared by")}: ____________    {Label(d, "Received by")}: ____________    {Label(d, "Stamp")}: ____________", false));
                    }

                    if (t.ShowDocumentNumber) list.Add((d.Number, false));
                    if (d.PrintedAt is not null) list.Add((d.PrintedAt, false));
                    break;
            }
        }

        return list;
    }

    private static string Label(PrintedDocument d, string key) => d.Labels.TryGetValue(key, out var v) ? v : key;
}
