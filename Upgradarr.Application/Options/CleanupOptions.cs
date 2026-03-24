namespace Upgradarr.Application.Options;

public record CleanupOptions
{
    public const string SectionName = "Cleanup";

    public int MaxItemAgeDays { get; init; } = 30;
    public int MaxDownloadTimeHours { get; init; } = 96;
    public int CleanupIntervalMinutes { get; init; } = 1;
    public int FailedDownloadCleanupHours { get; init; } = 12;
}
