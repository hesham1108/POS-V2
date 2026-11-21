using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace StoresPOS.PL.Reports;

/// <summary>
/// A report as exportable data (Phase 23). Each report builds it from the SAME DTO the screen shows,
/// so the Excel / CSV numbers always match the page.
/// </summary>
public sealed class ReportExport
{
    public string FileName { get; init; } = "report";
    public string Title { get; init; } = string.Empty;
    public string StoreName { get; init; } = string.Empty;
    public bool RightToLeft { get; init; }

    /// <summary>Lines under the title: period, filters, printed at.</summary>
    public List<string> Header { get; } = [];

    /// <summary>Summary figures (label, value) shown above the tables on the first sheet.</summary>
    public List<(string Label, object? Value)> Summary { get; } = [];

    public List<ReportSheet> Sheets { get; } = [];

    public ReportSheet AddSheet(string name, params string[] columns)
    {
        var sheet = new ReportSheet(name, columns);
        Sheets.Add(sheet);
        return sheet;
    }

    public static bool Wants(HttpRequest request, out string format)
    {
        format = request.Query["export"].ToString().ToLowerInvariant();
        return format is "xlsx" or "csv";
    }

    public FileContentResult ToFile(string format) =>
        format == "csv" ? ToCsv() : ToXlsx();

    private string Stamp => DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);

    public FileContentResult ToXlsx()
    {
        using var wb = new XLWorkbook();
        var first = true;
        foreach (var sheet in Sheets.DefaultIfEmpty(new ReportSheet("Report", [])))
        {
            var ws = wb.Worksheets.Add(SafeSheetName(sheet.Name, wb));
            ws.RightToLeft = RightToLeft;
            var row = 1;
            if (first)
            {
                ws.Cell(row, 1).Value = StoreName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 14;
                row++;
                ws.Cell(row, 1).Value = Title;
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;
                foreach (var line in Header)
                {
                    ws.Cell(row++, 1).Value = line;
                }

                if (Summary.Count > 0)
                {
                    row++;
                    foreach (var (label, value) in Summary)
                    {
                        ws.Cell(row, 1).Value = label;
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        SetValue(ws.Cell(row, 2), value);
                        row++;
                    }
                }

                row++;
            }

            var headerRow = row;
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = sheet.Columns[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF0F3");
            }

            row++;
            foreach (var values in sheet.Rows)
            {
                for (var c = 0; c < values.Length; c++)
                {
                    SetValue(ws.Cell(row, c + 1), values[c]);
                }

                row++;
            }

            if (sheet.Total is { } total)
            {
                for (var c = 0; c < total.Length; c++)
                {
                    SetValue(ws.Cell(row, c + 1), total[c]);
                    ws.Cell(row, c + 1).Style.Font.Bold = true;
                }
            }

            if (sheet.Columns.Count > 0)
            {
                ws.Range(headerRow, 1, Math.Max(headerRow, row - 1), sheet.Columns.Count).SetAutoFilter();
                ws.SheetView.FreezeRows(headerRow);
                ws.Columns(1, sheet.Columns.Count).AdjustToContents(headerRow, row, 8, 60);
            }

            first = false;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileContentResult(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = $"{FileName}-{Stamp}.xlsx"
        };
    }

    /// <summary>CSV of the main (first) sheet with the title and summary on top. UTF-8 with BOM for Excel.</summary>
    public FileContentResult ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Escape(Title));
        foreach (var line in Header)
        {
            sb.AppendLine(Escape(line));
        }

        foreach (var (label, value) in Summary)
        {
            sb.AppendLine(Escape(label) + "," + Escape(Format(value)));
        }

        foreach (var sheet in Sheets)
        {
            sb.AppendLine();
            sb.AppendLine(Escape(sheet.Name));
            sb.AppendLine(string.Join(",", sheet.Columns.Select(Escape)));
            foreach (var values in sheet.Rows)
            {
                sb.AppendLine(string.Join(",", values.Select(v => Escape(Format(v)))));
            }

            if (sheet.Total is { } total)
            {
                sb.AppendLine(string.Join(",", total.Select(v => Escape(Format(v)))));
            }
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FileContentResult(bytes, "text/csv; charset=utf-8") { FileDownloadName = $"{FileName}-{Stamp}.csv" };
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = Blank.Value;
                break;
            case decimal m:
                cell.Value = m;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case double d:
                cell.Value = d;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.NumberFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
                break;
            case bool b:
                cell.Value = b;
                break;
            default:
                // Text is stored as text: a leading "=" never becomes a formula.
                cell.Value = value.ToString();
                break;
        }
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static string SafeSheetName(string name, XLWorkbook wb)
    {
        var clean = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Sheet";
        if (clean.Length > 31) clean = clean[..31];
        var candidate = clean;
        var n = 2;
        while (wb.Worksheets.Any(w => w.Name == candidate))
        {
            var suffix = " " + n++;
            candidate = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }

        return candidate;
    }
}

public sealed class ReportSheet
{
    public ReportSheet(string name, IReadOnlyList<string> columns)
    {
        Name = name;
        Columns = columns;
    }

    public string Name { get; }
    public IReadOnlyList<string> Columns { get; }
    public List<object?[]> Rows { get; } = [];
    public object?[]? Total { get; set; }

    public ReportSheet Add(params object?[] values)
    {
        Rows.Add(values);
        return this;
    }
}
