using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Radarr.Models;

public record MovieResource
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? Overview { get; init; }
    public DateTimeOffset? InCinemas { get; init; }
    public DateTimeOffset? PhysicalRelease { get; init; }
    public DateTimeOffset? DigitalRelease { get; init; }
    public DateTimeOffset? ReleaseDate { get; init; }
    public int TmdbId { get; init; }
    public string? ImdbId { get; init; }
    public int Year { get; init; }
    public int Runtime { get; init; }
    public string? Status { get; init; }
    public bool Monitored { get; init; }
    public string? Path { get; init; }
    public int QualityProfileId { get; init; }
    public string? RootFolderPath { get; init; }
    public string? Folder { get; init; }
    public IEnumerable<string>? Genres { get; init; }
    public IEnumerable<MediaCover>? Images { get; init; }
    public string? RemotePoster { get; init; }
    public IEnumerable<int>? Tags { get; init; }
    public bool HasFile { get; init; }
    public MovieFileResource? MovieFile { get; init; }
    public string? Website { get; init; }
    public string? Certification { get; init; }
    public string? CleanTitle { get; init; }
    public object? OriginalLanguage { get; init; }
    public DateTimeOffset? Added { get; init; }
    public Ratings? Ratings { get; init; }
    public string? Minimumavailability { get; init; }
    public string? TitleSlug { get; init; }
    public bool IsAvailable { get; init; }
}

public record MovieFileResource
{
    public int Id { get; init; }
    public int MovieId { get; init; }
    public string? RelativePath { get; init; }
    public string? Path { get; init; }
    public long Size { get; init; }
    public DateTimeOffset? DateAdded { get; init; }
    public string? SceneName { get; init; }
    public string? ReleaseGroup { get; init; }
    public string? Edition { get; init; }
    public IEnumerable<Language>? Languages { get; init; }
    public QualityModel? Quality { get; init; }
    public IEnumerable<CustomFormatResource>? CustomFormats { get; init; }
    public int? CustomFormatScore { get; init; }
}

public record Ratings
{
    public RatingChild? Imdb { get; init; }
    public RatingChild? Tmdb { get; init; }
    public RatingChild? Metacritic { get; init; }
    public RatingChild? RottenTomatoes { get; init; }
    public RatingChild? Trakt { get; init; }
}

public record RatingChild
{
    public int Votes { get; init; }
    public double Value { get; init; }
    public string? Type { get; init; }
}
