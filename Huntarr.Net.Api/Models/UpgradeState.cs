namespace Huntarr.Net.Api.Models;

public class UpgradeState
{
    public int Id { get; set; }

    /// <summary>
    /// The ID of the series or movie
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// The title of the series or movie
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Type of item: series, season, episode or movie
    /// </summary>
    public required ItemType ItemType { get; set; }

    /// <summary>
    /// For season/episode items - the parent series ID
    /// </summary>
    public int? ParentSeriesId { get; set; }

    /// <summary>
    /// For episode items - the season number
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// For episode items - the episode number
    /// </summary>
    public int? EpisodeNumber { get; set; }

    /// <summary>
    /// The search state: pending, searched, or failed
    /// </summary>
    public required SearchState SearchState { get; set; }

    /// <summary>
    /// Whether the item is currently being monitored
    /// </summary>
    public bool IsMonitored { get; set; } = true;

    /// <summary>
    /// Position in the queue for processing
    /// </summary>
    public int QueuePosition { get; set; }

    /// <summary>
    /// Release date to check if item has been released
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Whether this item is missing (has no file with id 0 or null)
    /// </summary>
    public bool IsMissing { get; set; }

    /// <summary>
    /// Whether this item has completed all search/upgrade attempts
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this record was last updated
    /// </summary>
    public DateTimeOffset? LastUpdatedAt { get; set; }

    // Helper methods
    /// <summary>
    /// Checks if the item has been released (for filtering unreleased items)
    /// </summary>
    public bool IsReleased()
    {
        if (!ReleaseDate.HasValue)
            return true; // If no release date, assume released

        return ReleaseDate.Value <= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets a string key for this queue item
    /// </summary>
    public string GetKey()
    {
        return ItemType switch
        {
            ItemType.Episode => $"{ParentSeriesId}:s{SeasonNumber}e{EpisodeNumber}",
            ItemType.Season => $"{ParentSeriesId}:s{SeasonNumber}",
            _ => ItemId.ToString(),
        };
    }
}
