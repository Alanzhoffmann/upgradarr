namespace Upgradarr.Domain.Enums;

public enum QueueStatus
{
    Unknown,
    Queued,
    Paused,
    Downloading,
    Completed,
    Failed,
    Warning,
    Delay,
    DownloadClientUnavailable,
    Fallback,
}
