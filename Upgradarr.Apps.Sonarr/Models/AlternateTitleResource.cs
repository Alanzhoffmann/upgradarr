namespace Upgradarr.Apps.Sonarr.Models;

public record AlternateTitleResource
{
    public string? Comment { get; init; }
    public string? SceneOrigin { get; init; }
    public int? SceneSeasonNumber { get; init; }
    public int? SeasonNumber { get; init; }
    public string? Title { get; init; }
}
