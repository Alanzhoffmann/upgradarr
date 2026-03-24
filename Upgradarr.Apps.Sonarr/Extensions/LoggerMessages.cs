using Microsoft.Extensions.Logging;

namespace Upgradarr.Apps.Sonarr.Extensions;

public static partial class LoggerMessages
{
    [LoggerMessage(EventId = 4014, Level = LogLevel.Warning, Message = "Series {SeriesId} not found")]
    public static partial void LogSeriesNotFound(this ILogger logger, int seriesId);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "Searching for series: {SeriesTitle}")]
    public static partial void LogSearchingForSeries(this ILogger logger, string seriesTitle);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Searching for series {SeriesTitle} season {Season}")]
    public static partial void LogSearchingForSeason(this ILogger logger, string seriesTitle, int season);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Information, Message = "Searching for series {SeriesTitle} S{Season}E{Episode}")]
    public static partial void LogSearchingForEpisode(this ILogger logger, string seriesTitle, string season, string episode);

    [LoggerMessage(EventId = 4015, Level = LogLevel.Error, Message = "Error processing series upgrade for {SeriesId}")]
    public static partial void LogErrorProcessingSeriesUpgrade(this ILogger logger, Exception ex, int seriesId);

    [LoggerMessage(EventId = 4024, Level = LogLevel.Error, Message = "Error processing season upgrade for series {SeriesId} season {Season}")]
    public static partial void LogErrorProcessingSeasonUpgrade(this ILogger logger, Exception ex, int seriesId, int season);

    [LoggerMessage(EventId = 4025, Level = LogLevel.Error, Message = "Error processing episode upgrade for episode {EpisodeId}")]
    public static partial void LogErrorProcessingEpisodeUpgrade(this ILogger logger, Exception ex, int episodeId);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Information, Message = "Series {SeriesTitle} (ID: {SeriesId}) has ongoing downloads, skipping")]
    public static partial void LogSeriesHasOngoingDownloads(this ILogger logger, string seriesTitle, int seriesId);

    [LoggerMessage(
        EventId = 1023,
        Level = LogLevel.Information,
        Message = "Season {Season} in series {SeriesTitle} (ID: {SeriesId}) has ongoing downloads, skipping"
    )]
    public static partial void LogSeasonHasOngoingDownloads(this ILogger logger, int season, string seriesTitle, int seriesId);

    [LoggerMessage(
        EventId = 1024,
        Level = LogLevel.Information,
        Message = "Episode {SeriesTitle} S{SeasonNumber:D2}E{EpisodeNumber:D2} (ID: {EpisodeId}) is currently downloading, skipping"
    )]
    public static partial void LogEpisodeIsDownloading(this ILogger logger, string seriesTitle, int seasonNumber, int episodeNumber, int episodeId);

    [LoggerMessage(EventId = 1026, Level = LogLevel.Information, Message = "Series {SeriesTitle} (ID: {SeriesId}) is no longer monitored, removing from queue")]
    public static partial void LogSeriesNoLongerMonitored(this ILogger logger, string seriesTitle, int seriesId);

    [LoggerMessage(
        EventId = 1028,
        Level = LogLevel.Information,
        Message = "Episode {SeriesTitle} S{SeasonNumber:D2}E{EpisodeNumber:D2} (ID: {EpisodeId}) not found or unmonitored, removing from queue"
    )]
    public static partial void LogEpisodeNotFoundOrUnmonitored(this ILogger logger, string seriesTitle, int seasonNumber, int episodeNumber, int episodeId);
}
