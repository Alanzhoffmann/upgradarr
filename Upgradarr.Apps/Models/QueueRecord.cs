using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Models;

public class QueueRecord
{
    public required string DownloadId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? RemoveAt { get; private set; }
    public DateTimeOffset Added { get; init; } = DateTimeOffset.UtcNow;
    public RecordSource Source { get; set; }
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
