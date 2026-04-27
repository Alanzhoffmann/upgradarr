namespace Upgradarr.Integrations.Sonarr.Models;

public record EpisodeSearchCommand
{
    public string Name { get; init; } = "EpisodeSearch";
    public required IList<int> EpisodeIds { get; init; }
}
