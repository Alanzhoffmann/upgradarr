namespace Huntarr.Net.Clients.Models;

public class MoviesSearchCommand
{
    public string Name { get; } = "MoviesSearch";
    public required IList<int> MovieIds { get; init; }
}
