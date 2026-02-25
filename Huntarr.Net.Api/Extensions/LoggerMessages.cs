using Huntarr.Net.Api.Models;
using Huntarr.Net.Clients.Options;

namespace Huntarr.Net.Api.Extensions;

public static partial class LoggerMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Upgrade background service is starting.")]
    public static partial void LogStartingUpgradeBackgroundService(this ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Upgrade background service is stopping.")]
    public static partial void LogStoppingUpgradeBackgroundService(this ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Upgrade background service is checking for upgrades.")]
    public static partial void LogCheckingForUpgrades(this ILogger logger);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "An error occurred while checking for upgrades: {ErrorMessage}")]
    public static partial void LogErrorCheckingForUpgrades(this ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Sonarr options {SonarrOptions}")]
    public static partial void LogSonarrOptions(this ILogger logger, SonarrOptions sonarrOptions);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Sonarr system version: {Version}")]
    public static partial void LogSonarrSystemVersion(this ILogger logger, string version);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error, Message = "Could not retrieve Sonarr system information.")]
    public static partial void LogErrorRetrievingSonarrSystemInfo(this ILogger logger);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Information,
        Message = "Missing episode: {SeriesTitle} - S{SeasonNumber:D2}E{EpisodeNumber:D2} (ID: {EpisodeId})"
    )]
    public static partial void LogMissingEpisode(this ILogger logger, string seriesTitle, int seasonNumber, int episodeNumber, int episodeId);

    // Upgrade Service Logger Messages
    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Starting UpgradeBackgroundService")]
    public static partial void LogStartingUpgradeService(this ILogger logger);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Stopping UpgradeBackgroundService")]
    public static partial void LogStoppingUpgradeService(this ILogger logger);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "No more items to upgrade")]
    public static partial void LogNoMoreItemsToUpgrade(this ILogger logger);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Information, Message = "Processing {ItemType}: {Title} (ID: {ItemId})")]
    public static partial void LogProcessingItem(this ILogger logger, ItemType itemType, string title, int itemId);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Information, Message = "Item {Title} (ID: {ItemId}) has ongoing download, skipping")]
    public static partial void LogItemHasOngoingDownload(this ILogger logger, string title, int itemId);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Error, Message = "Error initializing upgrade states")]
    public static partial void LogErrorInitializingUpgradeStates(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Error, Message = "Error in upgrade background service")]
    public static partial void LogErrorInUpgradeBackgroundService(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4012, Level = LogLevel.Error, Message = "Error checking queue for item {ItemId}")]
    public static partial void LogErrorCheckingQueueForItem(this ILogger logger, Exception ex, int itemId);

    [LoggerMessage(EventId = 4013, Level = LogLevel.Warning, Message = "UpgradeState not found for series {SeriesId}")]
    public static partial void LogUpgradeStateNotFoundForSeries(this ILogger logger, int seriesId);

    [LoggerMessage(EventId = 4014, Level = LogLevel.Warning, Message = "Series {SeriesId} not found")]
    public static partial void LogSeriesNotFound(this ILogger logger, int seriesId);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "Searching for series: {SeriesTitle}")]
    public static partial void LogSearchingForSeries(this ILogger logger, string seriesTitle);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Searching for series {SeriesTitle} season {Season}")]
    public static partial void LogSearchingForSeason(this ILogger logger, string seriesTitle, int season);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Information, Message = "Searching for series {SeriesTitle} S{Season}E{Episode}")]
    public static partial void LogSearchingForEpisode(this ILogger logger, string seriesTitle, string season, string episode);

    [LoggerMessage(EventId = 1018, Level = LogLevel.Information, Message = "Completed all searches for series {SeriesTitle}")]
    public static partial void LogCompletedAllSearchesForSeries(this ILogger logger, string seriesTitle);

    [LoggerMessage(EventId = 4015, Level = LogLevel.Error, Message = "Error processing series upgrade for {SeriesId}")]
    public static partial void LogErrorProcessingSeriesUpgrade(this ILogger logger, Exception ex, int seriesId);

    [LoggerMessage(EventId = 4016, Level = LogLevel.Warning, Message = "UpgradeState not found for movie {MovieId}")]
    public static partial void LogUpgradeStateNotFoundForMovie(this ILogger logger, int movieId);

    [LoggerMessage(EventId = 4017, Level = LogLevel.Warning, Message = "Movie {MovieId} not found")]
    public static partial void LogMovieNotFound(this ILogger logger, int movieId);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Searching for movie: {MovieTitle}")]
    public static partial void LogSearchingForMovie(this ILogger logger, string movieTitle);

    [LoggerMessage(EventId = 4018, Level = LogLevel.Error, Message = "Error processing movie upgrade for {MovieId}")]
    public static partial void LogErrorProcessingMovieUpgrade(this ILogger logger, Exception ex, int movieId);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "Initialized upgrade states for {SeriesCount} series and {MovieCount} movies")]
    public static partial void LogInitializedUpgradeStates(this ILogger logger, int seriesCount, int movieCount);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Information, Message = "Reset all upgrade states")]
    public static partial void LogResetAllUpgradeStates(this ILogger logger);

    [LoggerMessage(EventId = 4019, Level = LogLevel.Error, Message = "Error resetting upgrade states")]
    public static partial void LogErrorResettingUpgradeStates(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4020, Level = LogLevel.Error, Message = "Error executing series search for {SeriesId}")]
    public static partial void LogErrorExecutingSeriesSearch(this ILogger logger, Exception ex, int seriesId);

    [LoggerMessage(EventId = 4021, Level = LogLevel.Error, Message = "Error executing season search for series {SeriesId} season {Season}")]
    public static partial void LogErrorExecutingSeasonSearch(this ILogger logger, Exception ex, int seriesId, int season);

    [LoggerMessage(EventId = 4022, Level = LogLevel.Error, Message = "Error executing episode search")]
    public static partial void LogErrorExecutingEpisodeSearch(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4023, Level = LogLevel.Error, Message = "Error executing movie search")]
    public static partial void LogErrorExecutingMovieSearch(this ILogger logger, Exception ex);

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

    [LoggerMessage(EventId = 1025, Level = LogLevel.Information, Message = "Movie {MovieTitle} (ID: {MovieId}) is currently downloading, skipping")]
    public static partial void LogMovieIsDownloading(this ILogger logger, string movieTitle, int movieId);

    [LoggerMessage(EventId = 1026, Level = LogLevel.Information, Message = "Series {SeriesTitle} (ID: {SeriesId}) is no longer monitored, removing from queue")]
    public static partial void LogSeriesNoLongerMonitored(this ILogger logger, string seriesTitle, int seriesId);

    [LoggerMessage(EventId = 1027, Level = LogLevel.Information, Message = "Movie {MovieTitle} (ID: {MovieId}) is no longer monitored, removing from queue")]
    public static partial void LogMovieNoLongerMonitored(this ILogger logger, string movieTitle, int movieId);

    [LoggerMessage(
        EventId = 1028,
        Level = LogLevel.Information,
        Message = "Episode {SeriesTitle} S{SeasonNumber:D2}E{EpisodeNumber:D2} (ID: {EpisodeId}) not found or unmonitored, removing from queue"
    )]
    public static partial void LogEpisodeNotFoundOrUnmonitored(this ILogger logger, string seriesTitle, int seasonNumber, int episodeNumber, int episodeId);

    [LoggerMessage(EventId = 4024, Level = LogLevel.Error, Message = "Error processing season upgrade for series {SeriesId} season {Season}")]
    public static partial void LogErrorProcessingSeasonUpgrade(this ILogger logger, Exception ex, int seriesId, int season);

    [LoggerMessage(EventId = 4025, Level = LogLevel.Error, Message = "Error processing episode upgrade for episode {EpisodeId}")]
    public static partial void LogErrorProcessingEpisodeUpgrade(this ILogger logger, Exception ex, int episodeId);

    [LoggerMessage(EventId = 1029, Level = LogLevel.Information, Message = "Reset queue position")]
    public static partial void LogResetQueuePosition(this ILogger logger);

    [LoggerMessage(EventId = 4026, Level = LogLevel.Error, Message = "Error resetting queue")]
    public static partial void LogErrorResettingQueue(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4027, Level = LogLevel.Error, Message = "Error removing item {ItemId} from queue")]
    public static partial void LogErrorRemovingItemFromQueue(this ILogger logger, Exception ex, int itemId);

    [LoggerMessage(EventId = 4028, Level = LogLevel.Error, Message = "Error removing season {Season} from series {SeriesId} from queue")]
    public static partial void LogErrorRemovingSeasonFromQueue(this ILogger logger, Exception ex, int seriesId, int season);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Information, Message = "Added {ItemCount} items to front of queue")]
    public static partial void LogAddedItemsToFrontOfQueue(this ILogger logger, int itemCount);

    [LoggerMessage(EventId = 4029, Level = LogLevel.Error, Message = "Error adding items to queue")]
    public static partial void LogErrorAddingItemsToQueue(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Information, Message = "Added {ItemCount} new items to queue and reshuffled")]
    public static partial void LogAddedNewItemsToQueue(this ILogger logger, int itemCount);

    [LoggerMessage(EventId = 1032, Level = LogLevel.Information, Message = "Updated monitoring status for {ItemCount} items")]
    public static partial void LogUpdatedMonitoringStatus(this ILogger logger, int itemCount);

    [LoggerMessage(EventId = 4030, Level = LogLevel.Error, Message = "Error detecting and adding new items to queue")]
    public static partial void LogErrorAddingNewItemsToQueue(this ILogger logger, Exception ex);
}
