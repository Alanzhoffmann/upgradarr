namespace Upgradarr.Integrations.Sonarr.Models;

public class SeriesSearchCommand
{
    public string Name { get; init; } = "SeriesSearch";
    public required int SeriesId { get; init; }
}
