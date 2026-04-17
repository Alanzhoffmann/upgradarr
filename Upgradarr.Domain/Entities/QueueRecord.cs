using Upgradarr.Domain.Enums;
using Upgradarr.Domain.ValueObjects;

namespace Upgradarr.Domain.Entities;

public class QueueRecord
{
    public required string DownloadId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? RemoveAt { get; private set; }
    public DateTimeOffset Added { get; init; } = DateTimeOffset.UtcNow;
    public RecordSource Source { get; init; }
    public ICollection<QueueItemScore> ItemScores { get; init; } = [];

    public void MarkForRemoval(DateTimeOffset removeAt)
    {
        RemoveAt = removeAt;
    }

    public void ClearRemoval()
    {
        RemoveAt = null;
    }
}
