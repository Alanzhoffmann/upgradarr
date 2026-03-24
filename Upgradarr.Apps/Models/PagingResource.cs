using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Models;

public record PagingResource<T>
{
    private IEnumerable<T>? _records;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public IEnumerable<T> Records
    {
        get => _records ?? [];
        init => _records = value;
    }
    public SortDirection SortDirection { get; init; }
    public string? SortKey { get; init; }
    public int TotalRecords { get; init; }
}
