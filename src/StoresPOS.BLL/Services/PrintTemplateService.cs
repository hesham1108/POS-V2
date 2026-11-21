using System.Text.Json;
using Microsoft.Extensions.Options;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.Services;

/// <summary>Saved print templates (Phase 24).</summary>
public interface IPrintTemplateService
{
    /// <summary>The saved template, or the default built from the store settings.</summary>
    PrintTemplate Get(string document, string format, StoreSettings settings);

    bool IsCustomized(string document, string format);

    Task SaveAsync(PrintTemplate template, CancellationToken cancellationToken = default);

    Task ResetAsync(string document, string format, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps templates in <c>App_Data/print-templates.json</c> (one object per "document-format" key) and
/// in memory; every <see cref="Get"/> returns a copy, so callers can change it (e.g. a preview) safely.
/// </summary>
public sealed class PrintTemplateService : IPrintTemplateService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string? _path;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly object _gate = new();
    private Dictionary<string, PrintTemplate>? _cache;

    public PrintTemplateService(IOptions<StoreSettingsPersistenceOptions> paths)
    {
        _path = paths.Value.TemplatesFilePath;
    }

    public PrintTemplate Get(string document, string format, StoreSettings settings)
    {
        format = PrintTemplateCatalog.NormalizeFormat(format);
        var saved = Load().GetValueOrDefault(PrintTemplateCatalog.KeyOf(document, format));
        var template = saved is null
            ? PrintTemplateCatalog.Default(document, format, settings)
            : Copy(saved);
        template.Document = document;
        template.Format = format;
        return PrintTemplateCatalog.Normalize(template);
    }

    public bool IsCustomized(string document, string format) =>
        Load().ContainsKey(PrintTemplateCatalog.KeyOf(document, PrintTemplateCatalog.NormalizeFormat(format)));

    public async Task SaveAsync(PrintTemplate template, CancellationToken cancellationToken = default)
    {
        template.Format = PrintTemplateCatalog.NormalizeFormat(template.Format);
        if (!PrintTemplateCatalog.Exists(template.Document, template.Format))
        {
            throw new BusinessException("This document has no layout for that paper size.");
        }

        var clean = PrintTemplateCatalog.Normalize(Copy(template));
        await WriteAsync(all => all[clean.Key] = clean, cancellationToken);
    }

    public Task ResetAsync(string document, string format, CancellationToken cancellationToken = default) =>
        WriteAsync(all => all.Remove(PrintTemplateCatalog.KeyOf(document, PrintTemplateCatalog.NormalizeFormat(format))), cancellationToken);

    private async Task WriteAsync(Action<Dictionary<string, PrintTemplate>> change, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            throw new BusinessException("Store settings file path is not configured.");
        }

        await _write.WaitAsync(cancellationToken);
        try
        {
            var all = new Dictionary<string, PrintTemplate>(Load());
            change(all);
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(all, Json), cancellationToken);
            lock (_gate)
            {
                _cache = all;
            }
        }
        finally
        {
            _write.Release();
        }
    }

    private Dictionary<string, PrintTemplate> Load()
    {
        lock (_gate)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            try
            {
                _cache = !string.IsNullOrWhiteSpace(_path) && File.Exists(_path)
                    ? JsonSerializer.Deserialize<Dictionary<string, PrintTemplate>>(File.ReadAllText(_path), Json) ?? []
                    : [];
            }
            catch (JsonException)
            {
                _cache = []; // a damaged file falls back to the defaults instead of breaking printing
            }

            return _cache;
        }
    }

    private static PrintTemplate Copy(PrintTemplate t) =>
        JsonSerializer.Deserialize<PrintTemplate>(JsonSerializer.Serialize(t, Json), Json)!;
}
