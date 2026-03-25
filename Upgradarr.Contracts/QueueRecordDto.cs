namespace Upgradarr.Contracts;

public class QueueRecordDto
{
    public required string DownloadId { get; init; }
    public string? Title { get; init; }
    public string Source { get; init; } = "";
    public DateTimeOffset Added { get; init; }
    public DateTimeOffset? RemoveAt { get; init; }
}
