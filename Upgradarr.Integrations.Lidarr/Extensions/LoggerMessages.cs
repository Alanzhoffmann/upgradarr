using Microsoft.Extensions.Logging;

namespace Upgradarr.Integrations.Lidarr.Extensions;

public static partial class LoggerMessages
{
    [LoggerMessage(EventId = 5030, Level = LogLevel.Warning, Message = "Artist {ArtistId} not found")]
    public static partial void LogArtistNotFound(this ILogger logger, int artistId);

    [LoggerMessage(EventId = 5031, Level = LogLevel.Information, Message = "Searching for artist: {ArtistName}")]
    public static partial void LogSearchingForArtist(this ILogger logger, string artistName);

    [LoggerMessage(EventId = 5032, Level = LogLevel.Information, Message = "Searching for album: {AlbumTitle}")]
    public static partial void LogSearchingForAlbum(this ILogger logger, string albumTitle);

    [LoggerMessage(EventId = 5033, Level = LogLevel.Error, Message = "Error processing artist upgrade for {ArtistId}")]
    public static partial void LogErrorProcessingArtistUpgrade(this ILogger logger, Exception ex, int artistId);

    [LoggerMessage(EventId = 5034, Level = LogLevel.Error, Message = "Error processing album upgrade for album {AlbumId}")]
    public static partial void LogErrorProcessingAlbumUpgrade(this ILogger logger, Exception ex, int albumId);

    [LoggerMessage(EventId = 5035, Level = LogLevel.Information, Message = "Artist {ArtistName} (ID: {ArtistId}) has ongoing downloads, skipping")]
    public static partial void LogArtistHasOngoingDownloads(this ILogger logger, string artistName, int artistId);

    [LoggerMessage(EventId = 5036, Level = LogLevel.Information, Message = "Album {AlbumTitle} (ID: {AlbumId}) is currently downloading, skipping")]
    public static partial void LogAlbumIsDownloading(this ILogger logger, string albumTitle, int albumId);

    [LoggerMessage(EventId = 5037, Level = LogLevel.Information, Message = "Artist {ArtistName} (ID: {ArtistId}) is no longer monitored, removing from queue")]
    public static partial void LogArtistNoLongerMonitored(this ILogger logger, string artistName, int artistId);

    [LoggerMessage(EventId = 5038, Level = LogLevel.Information, Message = "Album {AlbumTitle} (ID: {AlbumId}) is no longer monitored, removing from queue")]
    public static partial void LogAlbumNoLongerMonitored(this ILogger logger, string albumTitle, int albumId);

    [LoggerMessage(EventId = 5039, Level = LogLevel.Error, Message = "Error deleting queue item {ItemId}")]
    public static partial void LogErrorDeletingQueueItem(this ILogger logger, Exception ex, int itemId);
}
