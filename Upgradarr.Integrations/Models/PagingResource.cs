using Upgradarr.Integrations.Enums;

namespace Upgradarr.Integrations.Models;

public record PagingResource<T>
{
    private IReadOnlyCollection<T>? _records;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public IReadOnlyCollection<T> Records
    {
        get => _records ?? [];
        init => _records = value;
    }
    public SortDirection SortDirection { get; init; }
    public string? SortKey { get; init; }
    public int TotalRecords { get; init; }
}
