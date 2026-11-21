namespace StoresPOS.PL.Printing;

public interface IWindowsPrinterService
{
    bool IsSupported { get; }
    IReadOnlyList<string> ListPrinters();
    string? GetWindowsDefault();
    bool SetWindowsDefault(string printerName);

    /// <summary>Drops the cached printer list so the next call asks Windows again ("Refresh" button).</summary>
    void Invalidate();
}

public sealed class PrinterListDto
{
    public bool IsSupported { get; set; }
    public string? WindowsDefault { get; set; }
    public string? StoredDefault { get; set; }
    public string? ThermalPrinter { get; set; }
    public string? A4Printer { get; set; }
    public IReadOnlyList<string> Printers { get; set; } = [];
    public string? Message { get; set; }
}
