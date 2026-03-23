using Huntarr.Net.Api.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Upgradarr.Apps.Enums;
using Upgradarr.Apps.Models;
using Upgradarr.Apps.Radarr;
using Upgradarr.Apps.Sonarr;

namespace Huntarr.Net.Api.Interceptors;

public class DeleteQueueItemInterceptor : ISaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public DeleteQueueItemInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (result.HasResult)
        {
            return result;
        }

        if (eventData.Context is not AppDbContext context)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow();
        var entries = context
            .ChangeTracker.Entries<QueueRecord>()
            .Where(e => e.Entity.RemoveAt.HasValue && e.Entity.RemoveAt.Value <= now)
            .GroupBy(e => e.Entity.Source);

        var upgradeService = context.GetService<UpgradeService>();

        foreach (var group in entries)
        {
            switch (group.Key)
            {
                case RecordSource.Sonarr:
                    await DeleteSonarrQueueItemsAsync(context, group.Select(e => e.Entity), upgradeService, cancellationToken);
                    break;
                case RecordSource.Radarr:
                    await DeleteRadarrQueueItemsAsync(context, group.Select(e => e.Entity), upgradeService, cancellationToken);
                    break;
            }
        }

        return result;
    }

    private static async Task DeleteRadarrQueueItemsAsync(
        AppDbContext context,
        IEnumerable<QueueRecord> enumerable,
        UpgradeService upgradeService,
        CancellationToken cancellationToken
    )
    {
        var radarrClient = context.GetService<RadarrClient>();
        foreach (var entity in enumerable)
        {
            bool allDeleted = true;
            var itemsToRequeu = new List<(ItemType, int, int?, int?, int?)>();

            foreach (var itemScore in entity.ItemScores)
            {
                if (
                    !await radarrClient.DeleteQueueItemAsync(
                        itemScore.ItemId,
                        removeFromClient: true,
                        blocklist: true,
                        skipRedownload: true,
                        cancellationToken: cancellationToken
                    )
                )
                {
                    allDeleted = false;
                }
                else
                {
                    // Add movie to re-queue
                    itemsToRequeu.Add((ItemType.Movie, itemScore.ItemId, null, null, null));
                }
            }

            if (allDeleted)
            {
                context.Remove(entity);

                // Add items to front of upgrade queue
                if (itemsToRequeu.Count > 0)
                {
                    await upgradeService.AddItemsToFrontOfQueueAsync(itemsToRequeu, cancellationToken);
                }
            }
        }
    }

    private static async Task DeleteSonarrQueueItemsAsync(
        AppDbContext context,
        IEnumerable<QueueRecord> entities,
        UpgradeService upgradeService,
        CancellationToken cancellationToken
    )
    {
        var sonarrClient = context.GetService<SonarrClient>();
        foreach (var entity in entities)
        {
            bool allDeleted = true;
            var itemsToRequeue = new List<(ItemType, int, int?, int?, int?)>();

            foreach (var itemScore in entity.ItemScores)
            {
                if (
                    !await sonarrClient.DeleteQueueItemAsync(
                        itemScore.ItemId,
                        removeFromClient: true,
                        blocklist: true,
                        skipRedownload: true,
                        cancellationToken: cancellationToken
                    )
                )
                {
                    allDeleted = false;
                }
                else
                {
                    // Get episode details to determine show/season/episode to re-queue
                    try
                    {
                        var episodes = await sonarrClient.GetEpisodesAsync(episodeIds: [itemScore.ItemId], cancellationToken: cancellationToken);
                        var episode = episodes.FirstOrDefault();

                        if (episode is not null)
                        {
                            // Add show, season, and episode to re-queue
                            var seriesId = episode.SeriesId;

                            // Get series to add it back
                            var series = await sonarrClient.GetSeriesByIdAsync(seriesId, cancellationToken: cancellationToken);
                            if (series is not null && series.Monitored)
                            {
                                itemsToRequeue.Add((ItemType.Series, seriesId, null, null, null));
                                itemsToRequeue.Add((ItemType.Season, episode.SeasonNumber, seriesId, episode.SeasonNumber, null));
                                itemsToRequeue.Add((ItemType.Episode, episode.Id, seriesId, episode.SeasonNumber, episode.EpisodeNumber));
                            }
                        }
                    }
                    catch
                    {
                        // If we can't get episode details, just add the episode ID back
                        itemsToRequeue.Add((ItemType.Episode, itemScore.ItemId, null, null, null));
                    }
                }
            }

            if (allDeleted)
            {
                context.Remove(entity);

                // Add items to front of upgrade queue
                if (itemsToRequeue.Count > 0)
                {
                    await upgradeService.AddItemsToFrontOfQueueAsync(itemsToRequeue, cancellationToken);
                }
            }
        }
    }
}
