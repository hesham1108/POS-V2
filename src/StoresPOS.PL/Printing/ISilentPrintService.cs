namespace StoresPOS.PL.Printing;

public interface ISilentPrintService
{
    /// <summary>Print a completed sale to the configured Windows printer with no browser dialog.</summary>
    Task<SilentPrintResult> PrintSaleAsync(int invoiceId, string? format, CancellationToken cancellationToken = default);

    /// <summary>Print a sample receipt (Settings test buttons). printerName = test that printer.</summary>
    SilentPrintResult PrintSample(string? format, string? printerName = null);
}

public sealed class SilentPrintResult
{
    public bool Succeeded { get; init; }
    public string? PrinterName { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool UsedBrowserFallback { get; init; }

    public static SilentPrintResult Ok(string printer, string message) => new()
    {
        Succeeded = true,
        PrinterName = printer,
        Message = message
    };

    public static SilentPrintResult Fail(string message, bool browserFallback = true) => new()
    {
        Succeeded = false,
        Message = message,
        UsedBrowserFallback = browserFallback
    };
}
