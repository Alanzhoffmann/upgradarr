namespace Upgradarr.Integrations.Lidarr.Models;

public record ArtistSearchCommand
{
    public string Name { get; } = "ArtistSearch";
    public required int ArtistId { get; init; }
}
