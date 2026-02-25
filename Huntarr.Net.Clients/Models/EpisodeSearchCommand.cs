namespace Huntarr.Net.Clients.Models;

public class EpisodeSearchCommand
{
    public string Name { get; init; } = "EpisodeSearch";
    public required IList<int> EpisodeIds { get; init; }
}
