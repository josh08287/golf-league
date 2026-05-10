namespace GolfLeague.Application.Common;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// A user-supplied sort instruction parsed from a query string. The
/// <see cref="Column"/> is matched against a per-handler whitelist; an
/// unknown column or a null request causes the handler to fall back to
/// its default sort.
/// </summary>
public sealed record SortRequest(string Column, SortDirection Direction)
{
    /// <summary>
    /// Parse a `?sortBy=col&amp;sortDir=asc|desc` pair. Returns null if
    /// <paramref name="column"/> is null/empty so the handler picks its
    /// default.
    /// </summary>
    public static SortRequest? TryParse(string? column, string? direction)
    {
        if (string.IsNullOrWhiteSpace(column)) return null;
        // The "default" sentinel lets the FE explicitly request the
        // handler's multi-column default sort when the user has cycled
        // a column back to "unsorted".
        if (string.Equals(column, "default", StringComparison.OrdinalIgnoreCase)) return null;
        var dir = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;
        return new SortRequest(column.Trim(), dir);
    }
}

/// <summary>
/// Per-query sort whitelist + applier. Handlers register one
/// <see cref="SortMap{T}"/> per result type so column names users can
/// pass via <c>?sortBy=…</c> map to actual ordering keys, and unknown
/// columns silently fall back to a default.
/// </summary>
/// <typeparam name="T">The DTO type being sorted.</typeparam>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<T, object?>> _columns =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<IEnumerable<T>, IOrderedEnumerable<T>> _defaultSort;

    public SortMap(Func<IEnumerable<T>, IOrderedEnumerable<T>> defaultSort)
    {
        _defaultSort = defaultSort;
    }

    /// <summary>
    /// Register a sortable column. The accessor returns the value used
    /// for the comparison (string, int, DateOnly, etc.). Null values
    /// always sort last regardless of direction.
    /// </summary>
    public SortMap<T> Add(string column, Func<T, object?> accessor)
    {
        _columns[column] = accessor;
        return this;
    }

    /// <summary>
    /// Apply the user's sort if the column is whitelisted; otherwise
    /// fall back to the handler's default ordering.
    /// </summary>
    public IReadOnlyList<T> Apply(IEnumerable<T> source, SortRequest? request)
    {
        if (request is null || !_columns.TryGetValue(request.Column, out var accessor))
            return _defaultSort(source).ToList();

        // NullLast wraps so nulls appear at the end of either direction
        // (matches the SQL-side default behavior most users expect).
        IEnumerable<T> ordered = request.Direction == SortDirection.Ascending
            ? source.OrderBy(x => accessor(x) is null).ThenBy(accessor, NullSafeComparer.Instance)
            : source.OrderBy(x => accessor(x) is null).ThenByDescending(accessor, NullSafeComparer.Instance);

        return ordered.ToList();
    }

    private sealed class NullSafeComparer : IComparer<object?>
    {
        public static readonly NullSafeComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return 1;
            if (y is null) return -1;
            if (x is IComparable cx) return cx.CompareTo(y);
            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
