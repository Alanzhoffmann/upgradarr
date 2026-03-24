namespace Upgradarr.Apps.Models;

public record TrackedDownloadStatusMessage
{
    public string? Title { get; init; }
    public string[]? Messages { get; init; }
}
