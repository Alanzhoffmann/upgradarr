namespace Upgradarr.Integrations.Lidarr.Models;

public record AlbumSearchCommand
{
    public string Name { get; } = "AlbumSearch";
    public required IList<int> AlbumIds { get; init; }
}
