namespace Huntarr.Net.Clients.Models;

public record SeriesResource
{
    public DateTimeOffset? Added { get; init; }

    // public object? AddOptions { get; init; }
    public string? AirTime { get; init; }
    public IEnumerable<AlternateTitleResource>? AlternateTitles { get; init; }
    public string? Certification { get; init; }
    public string? CleanTitle { get; init; }
    public bool Ended { get; init; }
    public bool? EpisodesChanged { get; init; }
    public DateTimeOffset? FirstAired { get; init; }
    public string? Folder { get; init; }
    public IEnumerable<string>? Genres { get; init; }
    public int Id { get; init; }
    public IEnumerable<MediaCover>? Images { get; init; }
    public string? ImdbId { get; init; }
    public DateTimeOffset? LastAired { get; init; }
    public bool Monitored { get; init; }
    public string? MonitorNewItems { get; init; }
    public string? Network { get; init; }
    public DateTimeOffset? NextAiring { get; init; }
    public object? OriginalLanguage { get; init; }
    public string? Overview { get; init; }
    public string? Path { get; init; }
    public DateTimeOffset? PreviousAiring { get; init; }
    public string? ProfileName { get; init; }
    public int QualityProfileId { get; init; }

    // public RatingsResource? Ratings { get; init; }
    public string? RemotePoster { get; init; }
    public string? RootFolderPath { get; init; }
    public int Runtime { get; init; }
    public bool SeasonFolder { get; init; }

    // public IEnumerable<SeasonResource>? Seasons { get; init; }
    public string? SeriesType { get; init; }
    public string? SortTitle { get; init; }

    // public StatisticsResource? Statistics { get; init; }
    public string? Status { get; init; }
    public IEnumerable<int>? Tags { get; init; }
    public string? Title { get; init; }
    public string? TitleSlug { get; init; }
    public int TmdbId { get; init; }
    public int TvdbId { get; init; }
    public int TvMazeId { get; init; }
    public int TvRageId { get; init; }
    public bool UseSceneNumbering { get; init; }
    public int Year { get; init; }
}
