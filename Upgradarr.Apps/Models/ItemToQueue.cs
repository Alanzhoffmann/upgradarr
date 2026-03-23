using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Models;

public record ItemToQueue(ItemType Type, int ItemId, int? SeriesId = null, int? SeasonNumber = null, int? EpisodeNumber = null);
