using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace StoresPOS.BLL.DTOs;

/// <summary>
/// Paging, sorting and search parameters shared by every list screen (Phase 20).
/// Query string: page, pageSize, sort, dir (asc|desc), q.
/// </summary>
public sealed class ListQuery
{
    public static readonly int[] PageSizes = [25, 50, 100];
    public const int MaxExportRows = 50_000;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public string? Q { get; set; }

    public bool Descending => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);

    public string? Term => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();

    /// <summary>Same filters and sort, every row (capped) — used by CSV export.</summary>
    public ListQuery ForExport() => new()
    {
        Page = 1,
        PageSize = MaxExportRows,
        Sort = Sort,
        Dir = Dir,
        Q = Q
    };
}

/// <summary>Non-generic view of a page (pager partial).</summary>
public interface IPagedResult
{
    int Page { get; }
    int PageSize { get; }
    int TotalCount { get; }
    int TotalPages { get; }
    string? Sort { get; }
    bool Descending { get; }
}

public sealed class PagedResult<T> : IPagedResult
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string? Sort { get; init; }
    public bool Descending { get; init; }

    /// <summary>Optional total of a money column over every matching row (not only this page).</summary>
    public decimal? Sum { get; init; }

    public int FirstRow => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastRow => Math.Min(TotalCount, Page * PageSize);
}

public static class PagingExtensions
{
    /// <summary>
    /// Applies the requested sort (only columns listed in <paramref name="sortable"/>; otherwise the query's
    /// own order is kept), counts, and returns one page. Runs on the database: count + one page of rows.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query,
        ListQuery list,
        IReadOnlyCollection<string> sortable,
        Expression<Func<T, decimal>>? sum = null,
        bool allowAnyPageSize = false,
        CancellationToken cancellationToken = default)
    {
        var pageSize = allowAnyPageSize
            ? Math.Clamp(list.PageSize, 1, ListQuery.MaxExportRows)
            : ListQuery.PageSizes.Contains(list.PageSize) ? list.PageSize : ListQuery.PageSizes[0];

        var sortKey = sortable.FirstOrDefault(s => string.Equals(s, list.Sort, StringComparison.OrdinalIgnoreCase));
        if (sortKey is not null)
        {
            query = OrderByProperty(query, sortKey, list.Descending);
        }

        var total = await CountAsync(query, cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(list.Page, 1, pages);

        decimal? sumValue = null;
        if (sum is not null)
        {
            sumValue = query.Provider is IAsyncQueryProvider
                ? await query.SumAsync(sum, cancellationToken)
                : query.Sum(sum);
        }

        var pageQuery = query.Skip((page - 1) * pageSize).Take(pageSize);
        var items = pageQuery.Provider is IAsyncQueryProvider
            ? await pageQuery.ToListAsync(cancellationToken)
            : pageQuery.ToList();

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Sort = sortKey,
            Descending = sortKey is not null && list.Descending,
            Sum = sumValue
        };
    }

    /// <summary>Pages an in-memory list with the same rules (for lists built in code).</summary>
    public static Task<PagedResult<T>> ToPagedAsync<T>(
        this IEnumerable<T> rows,
        ListQuery list,
        IReadOnlyCollection<string> sortable,
        Expression<Func<T, decimal>>? sum = null,
        bool allowAnyPageSize = false) =>
        rows.AsQueryable().ToPagedAsync(list, sortable, sum, allowAnyPageSize);

    private static async Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.Provider is IAsyncQueryProvider ? await query.CountAsync(cancellationToken) : query.Count();

    private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no property {propertyName}.");
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(body, parameter);
        var method = (descending ? OrderByDescendingMethod : OrderByMethod).MakeGenericMethod(typeof(T), property.PropertyType);
        return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
    }

    private static readonly MethodInfo OrderByMethod = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2);

    private static readonly MethodInfo OrderByDescendingMethod = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.OrderByDescending) && m.GetParameters().Length == 2);
}
