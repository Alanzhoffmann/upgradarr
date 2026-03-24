using Microsoft.Extensions.Logging;

namespace Upgradarr.Apps.Radarr.Extensions;

public static partial class LoggerMessages
{
    [LoggerMessage(EventId = 4017, Level = LogLevel.Warning, Message = "Movie {MovieId} not found")]
    public static partial void LogMovieNotFound(this ILogger logger, int movieId);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Searching for movie: {MovieTitle}")]
    public static partial void LogSearchingForMovie(this ILogger logger, string movieTitle);

    [LoggerMessage(EventId = 4018, Level = LogLevel.Error, Message = "Error processing movie upgrade for {MovieId}")]
    public static partial void LogErrorProcessingMovieUpgrade(this ILogger logger, Exception ex, int movieId);

    [LoggerMessage(EventId = 1025, Level = LogLevel.Information, Message = "Movie {MovieTitle} (ID: {MovieId}) is currently downloading, skipping")]
    public static partial void LogMovieIsDownloading(this ILogger logger, string movieTitle, int movieId);

    [LoggerMessage(EventId = 1027, Level = LogLevel.Information, Message = "Movie {MovieTitle} (ID: {MovieId}) is no longer monitored, removing from queue")]
    public static partial void LogMovieNoLongerMonitored(this ILogger logger, string movieTitle, int movieId);

    [LoggerMessage(EventId = 4019, Level = LogLevel.Error, Message = "Error deleting queue item {ItemId}")]
    public static partial void LogErrorDeletingQueueItem(this ILogger logger, Exception ex, int itemId);
}
