using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.Extensions;

/// <summary>Helpers shared by every list screen (Phase 20): query-string URLs and CSV export.</summary>
public static class ListUrl
{
    /// <summary>The current URL with some query values replaced (null or empty = removed).</summary>
    public static string With(HttpRequest request, params (string Key, string? Value)[] changes)
    {
        var values = request.Query.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value))
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        // Exports and print flags never carry over into navigation links.
        if (!changes.Any(c => string.Equals(c.Key, "export", StringComparison.OrdinalIgnoreCase)))
        {
            values.Remove("export");
        }

        values.Remove("print");
        var query = values.Where(v => !string.IsNullOrEmpty(v.Value))
            .Select(v => new KeyValuePair<string, string?>(v.Key, v.Value));
        return QueryHelpers.AddQueryString(request.Path, query);
    }

    /// <summary>Same filters, back to page 1 (used when a filter or the sort changes).</summary>
    public static string Reset(HttpRequest request, params (string Key, string? Value)[] changes) =>
        With(request, changes.Append(("page", null)).ToArray());

    public static string Export(HttpRequest request) => With(request, ("export", "csv"), ("page", null));

    /// <summary>
    /// Removable chips for the active filters. Boolean filters ("true") show the label only; text gives the
    /// display value for ids/enums (null = the raw value). Removing a chip goes back to page 1.
    /// </summary>
    public static IReadOnlyList<ViewModels.FilterChip> Chips(
        HttpRequest request,
        params (string Key, string Label, Func<string, string?>? Text)[] filters)
    {
        var chips = new List<ViewModels.FilterChip>();
        foreach (var (key, label, text) in filters)
        {
            var value = request.Query[key].ToString();
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var shown = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                ? label
                : $"{label}: {text?.Invoke(value) ?? value}";
            chips.Add(new ViewModels.FilterChip(shown, Reset(request, (key, null))));
        }

        return chips;
    }

    /// <summary>Chip for the search box (q, or the older "search" parameter).</summary>
    public static IEnumerable<ViewModels.FilterChip> SearchChip(HttpRequest request, string label)
    {
        var q = request.Query["q"].ToString();
        if (string.IsNullOrWhiteSpace(q))
        {
            q = request.Query["search"].ToString();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            yield return new ViewModels.FilterChip($"{label}: {q}", Reset(request, ("q", null), ("search", null)));
        }
    }
}

/// <summary>Writes rows as a UTF-8 CSV (with BOM so Excel shows Arabic correctly).</summary>
public static class CsvExport
{
    public static FileContentResult File<T>(
        string fileName,
        IEnumerable<T> rows,
        params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", columns.Select(c => Escape(Format(c.Value(row))))));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = $"{fileName}-{stamp}.csv"
        };
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value)
    {
        // Leading = + - @ would run as a formula in Excel.
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

public static class ListQueryExtensions
{
    public static bool WantsCsv(this HttpRequest request) =>
        string.Equals(request.Query["export"], "csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>The list for this request: the page asked for, or every row (capped) for a CSV export.</summary>
    public static ListQuery ForRequest(this ListQuery list, HttpRequest request) =>
        request.WantsCsv() ? list.ForExport() : list;
}

public static class ControllerLocalizerExtensions
{
    /// <summary>Shared localizer for controllers that do not inject one (CSV headers).</summary>
    public static Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> Localizer(this ControllerBase controller) =>
        controller.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<SharedResource>>();
}
