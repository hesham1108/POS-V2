using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.ViewModels;

/// <summary>Sortable column header (partial _SortTh).</summary>
public sealed record SortHeader(string Key, string Label, IPagedResult Page, bool Numeric = false);

/// <summary>A removable active filter shown as a chip in the list toolbar.</summary>
public sealed record FilterChip(string Label, string RemoveUrl);

/// <summary>Search, active filter chips, CSV export and the main add button above a list (partial _ListToolbar).</summary>
public sealed class ListToolbarModel
{
    public string? Q { get; init; }
    public string SearchPlaceholder { get; init; } = string.Empty;
    public IReadOnlyList<FilterChip> Chips { get; init; } = [];
    public IPagedResult? Page { get; init; }
    public bool ShowSearch { get; init; } = true;
    public bool ShowExport { get; init; } = true;
    public string? AddUrl { get; init; }
    public string? AddLabel { get; init; }
}
