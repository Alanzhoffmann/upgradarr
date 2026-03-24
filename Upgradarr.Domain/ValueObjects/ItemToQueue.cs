using Upgradarr.Domain.Enums;

namespace Upgradarr.Domain.ValueObjects;

public record ItemToQueue(ItemType Type, int ItemId, int? SeriesId = null, int? SeasonNumber = null, int? EpisodeNumber = null);
