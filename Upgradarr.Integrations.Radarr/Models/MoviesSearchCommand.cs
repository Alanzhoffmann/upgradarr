namespace Upgradarr.Integrations.Radarr.Models;

public record MoviesSearchCommand
{
    public string Name { get; } = "MoviesSearch";
    public required IList<int> MovieIds { get; init; }
}
