namespace Upgradarr.Application.Options;

public class CleanupOptions
{
    public const string SectionName = "Cleanup";

    public int MaxItemAgeDays { get; set; } = 30;
    public int MaxDownloadTimeHours { get; set; } = 96;
    public int CleanupIntervalMinutes { get; set; } = 3;
    public int FailedDownloadCleanupHours { get; set; } = 12;
}
