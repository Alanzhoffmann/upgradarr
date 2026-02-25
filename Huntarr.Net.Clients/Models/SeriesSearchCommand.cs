namespace Huntarr.Net.Clients.Models;

public class SeriesSearchCommand
{
    public string Name { get; init; } = "SeriesSearch";
    public required int SeriesId { get; init; }
}
