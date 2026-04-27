using Upgradarr.Integrations.Models;

namespace Upgradarr.Integrations.Radarr.Models;

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
