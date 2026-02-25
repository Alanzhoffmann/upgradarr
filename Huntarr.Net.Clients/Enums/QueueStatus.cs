namespace Huntarr.Net.Clients.Enums;

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
