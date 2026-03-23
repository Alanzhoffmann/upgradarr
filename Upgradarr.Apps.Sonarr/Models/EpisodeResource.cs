namespace Upgradarr.Apps.Sonarr.Models;

public record EpisodeResource
{
    public int? AbsoluteEpisodeNumber { get; set; }
    public int SeriesId { get; init; }
    public SeriesResource? Series { get; init; }
    public int TvdbId { get; init; }
    public int EpisodeFileId { get; init; }
    public EpisodeFileResource? EpisodeFile { get; init; }
    public int SeasonNumber { get; init; }
    public int EpisodeNumber { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? AirDateUtc { get; init; }
    public DateTimeOffset? LastSearchTime { get; init; }
    public int Runtime { get; init; }
    public string? FinaleType { get; init; }
    public string? Overview { get; init; }
    public bool HasFile { get; init; }
    public bool Monitored { get; init; }
    public bool UnverifiedSceneNumbering { get; init; }
    public int Id { get; init; }

    // endTime
    // Type:string | null
    // Format:date-time
    // the date-time notation as defined by RFC 3339, section 5.6, for example, 2017-07-21T17:32:28Z

    // grabDate
    // Type:string | null
    // Format:date-time
    // the date-time notation as defined by RFC 3339, section 5.6, for example, 2017-07-21T17:32:28Z

    // images
    // Type:array,null MediaCover
    // Show Child Attributesfor images

    // sceneAbsoluteEpisodeNumber
    // Type:integer | null
    // Format:int32
    // Signed 32-bit integers (commonly used integer type).

    // sceneEpisodeNumber
    // Type:integer | null
    // Format:int32
    // Signed 32-bit integers (commonly used integer type).

    // sceneSeasonNumber
    // Type:integer | null
    // Format:int32
    // Signed 32-bit integers (commonly used integer type).
}
