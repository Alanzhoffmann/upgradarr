namespace Upgradarr.Integrations.Models;

public record Ratings
{
    public Rating? Imdb { get; init; }
    public Rating? Tmdb { get; init; }
    public Rating? Metacritic { get; init; }
    public Rating? RottenTomatoes { get; init; }
    public Rating? Trakt { get; init; }
}

public record Rating
{
    public int Votes { get; init; }
    public double Value { get; init; }
    public string? Type { get; init; }
}
