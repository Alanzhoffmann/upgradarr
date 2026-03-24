namespace Upgradarr.Apps.Sonarr.Models;

public class SeasonSearchCommand
{
    public string Name { get; init; } = "SeasonSearch";
    public required int SeriesId { get; init; }
    public required int SeasonNumber { get; init; }
}
