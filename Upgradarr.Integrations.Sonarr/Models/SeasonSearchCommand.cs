namespace Upgradarr.Integrations.Sonarr.Models;

public record SeasonSearchCommand
{
    public string Name { get; init; } = "SeasonSearch";
    public required int SeriesId { get; init; }
    public required int SeasonNumber { get; init; }
}
