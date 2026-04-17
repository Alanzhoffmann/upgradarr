using Upgradarr.Domain.Enums;

namespace Upgradarr.Domain.Entities.Sonarr;

public class SonarrUpgradeState : UpgradeState
{
    public required SonarrMetadata Metadata { get; init; }

    public override (RecordSource, int, int?, int?, int?) GetUniqueKey() =>
        (
            RecordSource.Sonarr,
            ItemId,
            Metadata.ParentSeriesId,
            Metadata.Type == SonarrItemType.Episode ? Metadata.SeasonNumber : null,
            Metadata.Type == SonarrItemType.Episode ? Metadata.EpisodeNumber : null
        );
}

/// <summary>
/// Metadata specific to Sonarr items, used for determining what to search for and how to display the item
/// <param name="Type">The type of item: series, season, or episode</param>
/// <param name="ParentSeriesId">For season/episode items - the parent series ID</param>
/// <param name="SeasonNumber">For episode items - the season number</param>
/// <param name="EpisodeNumber">For episode items - the episode number</param>
/// </summary>
public record SonarrMetadata(SonarrItemType Type, int? ParentSeriesId = null, int? SeasonNumber = null, int? EpisodeNumber = null);

public enum SonarrItemType
{
    Series = 1,
    Season = 2,
    Episode = 3,
}
