using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Sonarr.Models;

public record EpisodeFileResource
{
    public int Id { get; init; }
    public int SeriesId { get; init; }
    public int SeasonNumber { get; init; }
    public string? RelativePath { get; init; }
    public string? Path { get; init; }
    public long Size { get; init; }
    public DateTime DateAdded { get; init; }
    public string? SceneName { get; init; }
    public string? ReleaseGroup { get; init; }
    public IList<Language>? Languages { get; init; }
    public QualityModel? Quality { get; init; }
    public int CustomFormatScore { get; init; }
    public int? IndexerFlags { get; init; }
    public string? ReleaseType { get; init; }
    public MediaInfoResource? MediaInfo { get; init; }
    public bool QualityCutoffNotMet { get; init; }
}
