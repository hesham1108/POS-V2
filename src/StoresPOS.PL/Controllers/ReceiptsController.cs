using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Entities;
using StoresPOS.PL.Printing;

namespace StoresPOS.PL.Controllers;

/// <summary>
/// Printable documents (80 mm / A4). Phase 24: every document is built once as a <see cref="PrintedDocument"/>
/// with its saved template (section order, switches, texts) and drawn by Document80 / DocumentA4; Windows
/// silent printing draws the same document. POS prefers silent print; the browser page is the fallback/preview.
/// </summary>
[Authorize]
public class ReceiptsController : Controller
{
    private readonly ISalesService _sales;
    private readonly IShiftService _shifts;
    private readonly IReturnService _returns;
    private readonly IPurchaseService _purchases;
    private readonly ISilentPrintService _silentPrint;
    private readonly IPaymentMethodService _methods;
    private readonly IPrintTemplateService _templates;
    private readonly PrintDocumentBuilder _builder;
    private readonly IOptionsMonitor<StoreSettings> _store;
    private readonly TimeProvider _time;

    public ReceiptsController(
        ISalesService sales,
        IShiftService shifts,
        IReturnService returns,
        IPurchaseService purchases,
        ISilentPrintService silentPrint,
        IPaymentMethodService methods,
        IPrintTemplateService templates,
        IStringLocalizer<SharedResource> localizer,
        IOptionsMonitor<StoreSettings> store,
        TimeProvider time)
    {
        _sales = sales;
        _shifts = shifts;
        _returns = returns;
        _purchases = purchases;
        _silentPrint = silentPrint;
        _methods = methods;
        _templates = templates;
        _builder = new PrintDocumentBuilder(localizer);
        _store = store;
        _time = time;
    }

    private StoreSettings Store => _store.CurrentValue;

    // ================================================================== sales

    [HttpGet]
    public Task<IActionResult> Sale(int id, bool autoprint, CancellationToken cancellationToken) =>
        SaleAsync(id, autoprint, PrintTemplateCatalog.Mm80, cancellationToken);

    /// <summary>A4 sales invoice for a regular printer (Phase 11).</summary>
    [HttpGet]
    public Task<IActionResult> SaleA4(int id, bool autoprint, CancellationToken cancellationToken) =>
        SaleAsync(id, autoprint, PrintTemplateCatalog.A4, cancellationToken);

    private async Task<IActionResult> SaleAsync(int id, bool autoprint, string format, CancellationToken cancellationToken)
    {
        var invoice = await _sales.GetInvoiceAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        // Back office may print any invoice; a cashier only invoices of the shift they are working in.
        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = User.HasPermission(AppPermissions.PosUse)
                ? await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken)
                : null;
            if (activeShift != invoice.ShiftId)
            {
                return Forbid();
            }
        }

        var returns = format == PrintTemplateCatalog.A4
            ? await _returns.GetListAsync(new SalesReturnFilterDto { InvoiceId = id }, cancellationToken)
            : null;
        var doc = await BuildSaleAsync(invoice, Store, Template(PrintTemplateCatalog.Sale, format), returns, cancellationToken);
        return Render(doc, autoprint);
    }

    // ================================================================== returns

    /// <summary>Return slip (80 mm) handed to the customer with the refund.</summary>
    [HttpGet]
    public Task<IActionResult> Return(int id, bool autoprint, CancellationToken cancellationToken) =>
        ReturnAsync(id, autoprint, PrintTemplateCatalog.Mm80, cancellationToken);

    [HttpGet]
    public Task<IActionResult> ReturnA4(int id, bool autoprint, CancellationToken cancellationToken) =>
        ReturnAsync(id, autoprint, PrintTemplateCatalog.A4, cancellationToken);

    private async Task<IActionResult> ReturnAsync(int id, bool autoprint, string format, CancellationToken cancellationToken)
    {
        var ret = await _returns.GetAsync(id, cancellationToken);
        if (ret is null)
        {
            return NotFound();
        }

        // Same rule as sale receipts: back office any, otherwise only returns of the user's current shift.
        if (!User.HasPermission(AppPermissions.ReportsView))
        {
            var activeShift = await _shifts.GetActiveShiftIdForUserAsync(this.GetRequiredUserId(), cancellationToken);
            if (!User.HasPermission(AppPermissions.ReturnsCreate) || activeShift is null || activeShift != ret.ShiftId)
            {
                return Forbid();
            }
        }

        var doc = await BuildReturnAsync(ret, Store, Template(PrintTemplateCatalog.Return, format), cancellationToken);
        return Render(doc, autoprint);
    }

    // ================================================================== purchases

    /// <summary>A4 purchase invoice.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.PurchasesManage)]
    public async Task<IActionResult> PurchaseA4(int id, bool autoprint, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.GetByIdAsync(id, cancellationToken);
        if (purchase is null)
        {
            return NotFound();
        }

        var format = new ReceiptFormat(Store);
        return Render(_builder.Purchase(purchase, format, Template(PrintTemplateCatalog.Purchase, PrintTemplateCatalog.A4)), autoprint);
    }

    // ================================================================== shift report

    /// <summary>
    /// Shift report. format = "80mm" | "A4"; empty = the Shift report default from Settings → Printing (Phase 19).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Shift(int id, bool autoprint, string? format, CancellationToken cancellationToken)
    {
        var resolved = SilentPrintService.ResolveFormat(Store, PrintDocumentType.Shift, format);
        return await ShiftReportAsync(id, autoprint, resolved, cancellationToken);
    }

    /// <summary>A4 shift report.</summary>
    [HttpGet]
    public Task<IActionResult> ShiftA4(int id, bool autoprint, CancellationToken cancellationToken) =>
        ShiftReportAsync(id, autoprint, PrintFormats.A4, cancellationToken);

    private async Task<IActionResult> ShiftReportAsync(int id, bool autoprint, string format, CancellationToken cancellationToken)
    {
        if (!User.HasPermission(AppPermissions.ReportsView) && !User.HasPermission(AppPermissions.ShiftsClose))
        {
            return Forbid();
        }

        var shift = await _shifts.GetSummaryAsync(id, cancellationToken);
        if (shift is null)
        {
            return NotFound();
        }

        var template = Template(PrintTemplateCatalog.Shift, PrintTemplateCatalog.NormalizeFormat(format));
        var doc = _builder.Shift(shift, new ReceiptFormat(Store), template, _time.GetUtcNow().UtcDateTime);
        return Render(doc, autoprint);
    }

    // ================================================================== silent print

    /// <summary>Silent Windows print to the selected/default printer (no browser dialog).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SilentPrintSale(int id, string? format, CancellationToken cancellationToken)
    {
        var result = await _silentPrint.PrintSaleAsync(id, format, cancellationToken);
        return Json(new
        {
            ok = result.Succeeded,
            printed = result.Succeeded,
            printerName = result.PrinterName,
            message = result.Message,
            fallbackUrl = Url.Action(
                SilentPrintService.ResolveFormat(Store, PrintDocumentType.Sale, format) == PrintFormats.A4
                    ? "SaleA4"
                    : "Sale",
                "Receipts",
                new { id })
        });
    }

    // ================================================================== previews (Settings)

    /// <summary>Sample 80 mm receipt for Settings printing preview (no database invoice).</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.SettingsManage)]
    public Task<IActionResult> Sample80(CancellationToken cancellationToken) => SampleAsync(PrintTemplateCatalog.Mm80, cancellationToken);

    /// <summary>Sample A4 invoice for Settings printing preview.</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.SettingsManage)]
    public Task<IActionResult> SampleA4(CancellationToken cancellationToken) => SampleAsync(PrintTemplateCatalog.A4, cancellationToken);

    private async Task<IActionResult> SampleAsync(string format, CancellationToken cancellationToken)
    {
        var settings = ApplyPreviewOverrides(SettingsMapper.Clone(Store), Request.Query);
        var template = _templates.Get(PrintTemplateCatalog.Sale, format, settings);
        var invoice = ReceiptSampleFactory.CreateSampleInvoice(settings);
        return Render(await BuildSaleAsync(invoice, settings, template, null, cancellationToken), autoprint: false);
    }

    /// <summary>
    /// Template designer preview (Phase 24): the posted, unsaved template applied to a sample of that document.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissions.SettingsManage)]
    public async Task<IActionResult> TemplatePreview(string template, CancellationToken cancellationToken)
    {
        PrintTemplate? t;
        try
        {
            t = JsonSerializer.Deserialize<PrintTemplate>(template ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            t = null;
        }

        if (t is null || !PrintTemplateCatalog.Exists(t.Document, PrintTemplateCatalog.NormalizeFormat(t.Format)))
        {
            return BadRequest();
        }

        t.Format = PrintTemplateCatalog.NormalizeFormat(t.Format);
        PrintTemplateCatalog.Normalize(t);
        var settings = Store;
        var format = new ReceiptFormat(settings);
        PrintedDocument doc = t.Document switch
        {
            PrintTemplateCatalog.Return => await BuildReturnAsync(ReceiptSampleFactory.CreateSampleReturn(settings), settings, t, cancellationToken),
            PrintTemplateCatalog.Purchase => _builder.Purchase(ReceiptSampleFactory.CreateSamplePurchase(), format, t),
            PrintTemplateCatalog.Shift => _builder.Shift(ReceiptSampleFactory.CreateSampleShift(), format, t, _time.GetUtcNow().UtcDateTime),
            _ => await BuildSaleAsync(ReceiptSampleFactory.CreateSampleInvoice(settings), settings, t, null, cancellationToken)
        };
        return Render(doc, autoprint: false);
    }

    // ================================================================== helpers

    private PrintTemplate Template(string document, string format) => _templates.Get(document, format, Store);

    private IActionResult Render(PrintedDocument doc, bool autoprint)
    {
        ViewData["AutoPrint"] = autoprint;
        return View(doc.Template.IsA4 ? "DocumentA4" : "Document80", doc);
    }

    private async Task<PrintedDocument> BuildSaleAsync(
        SalesInvoiceDetailsDto invoice,
        StoreSettings settings,
        PrintTemplate template,
        IReadOnlyList<SalesReturnListItemDto>? returns,
        CancellationToken cancellationToken)
    {
        var format = new ReceiptFormat(settings);
        var (name, isCash) = await MethodAsync(invoice.PaymentMethod, format.Language, cancellationToken);
        return _builder.Sale(invoice, format, template, name, isCash, Code128.ToSvg(invoice.InvoiceNumber), returns);
    }

    private async Task<PrintedDocument> BuildReturnAsync(
        SalesReturnDetailsDto ret,
        StoreSettings settings,
        PrintTemplate template,
        CancellationToken cancellationToken)
    {
        var format = new ReceiptFormat(settings);
        var (name, _) = await MethodAsync(ret.RefundMethod, format.Language, cancellationToken);
        return _builder.Return(ret, format, template, name, Code128.ToSvg(ret.ReturnNumber));
    }

    private async Task<(string Name, bool IsCash)> MethodAsync(PaymentMethod method, string language, CancellationToken cancellationToken)
    {
        var m = await _methods.GetAsync((byte)method, cancellationToken);
        return m is null ? (method.ToString(), method == PaymentMethod.Cash) : (m.NameFor(language), m.IsCash || method == PaymentMethod.Cash);
    }

    private static StoreSettings ApplyPreviewOverrides(StoreSettings s, IQueryCollection q)
    {
        if (q.TryGetValue("PaperWidthMm", out var paper) && int.TryParse(paper, out var mm) && mm is 58 or 80)
        {
            s.PaperWidthMm = mm;
        }

        if (q.TryGetValue("ReceiptLanguage", out var lang) && !string.IsNullOrWhiteSpace(lang))
        {
            s.ReceiptLanguage = lang.ToString() == "en" ? "en" : "ar";
        }

        if (q.TryGetValue("ReceiptDigits", out var digits) && !string.IsNullOrWhiteSpace(digits))
        {
            s.ReceiptDigits = digits.ToString() == "arabic" ? "arabic" : "western";
        }

        if (q.TryGetValue("ReceiptFontScale", out var scale) && decimal.TryParse(scale, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var sc))
        {
            s.ReceiptFontScale = Math.Clamp(sc, 0.9m, 1.2m);
        }

        if (q.TryGetValue("ReceiptFooterEn", out var fe))
        {
            s.ReceiptFooterEn = fe.ToString();
        }

        if (q.TryGetValue("ReceiptFooterAr", out var fa))
        {
            s.ReceiptFooterAr = fa.ToString();
        }

        s.ShowLogoOn80mm = ParseBool(q, "ShowLogoOn80mm", s.ShowLogoOn80mm);
        s.ShowBarcode = ParseBool(q, "ShowBarcode", s.ShowBarcode);
        s.ShowAddressOnReceipt = ParseBool(q, "ShowAddressOnReceipt", s.ShowAddressOnReceipt);
        s.ShowPhoneOnReceipt = ParseBool(q, "ShowPhoneOnReceipt", s.ShowPhoneOnReceipt);
        s.ShowTaxNumberOnReceipt = ParseBool(q, "ShowTaxNumberOnReceipt", s.ShowTaxNumberOnReceipt);
        s.A4ShowLogo = ParseBool(q, "A4ShowLogo", s.A4ShowLogo);
        s.A4ShowCommercialRegister = ParseBool(q, "A4ShowCommercialRegister", s.A4ShowCommercialRegister);
        return s;
    }

    private static bool ParseBool(IQueryCollection q, string key, bool fallback) =>
        q.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;
}
