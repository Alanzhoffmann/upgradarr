using Upgradarr.Domain.Enums;

namespace Upgradarr.Domain.Entities.Radarr;

public class RadarrUpgradeState : UpgradeState
{
    public required RadarrMetadata Metadata { get; init; }

    public override (RecordSource, int, int?, int?, int?) GetUniqueKey() => (RecordSource.Radarr, ItemId, null, null, null);
}

/// <summary>
/// Metadata specific to Radarr items, used for determining what to search for and how to display the item
/// </summary>
/// <param name="Type">The type of item: movie</param>
public record RadarrMetadata(RadarrItemType Type);

public enum RadarrItemType
{
    Movie = 1,
}
