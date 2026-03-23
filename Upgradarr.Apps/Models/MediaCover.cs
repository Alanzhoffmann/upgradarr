using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Models;

public record MediaCover
{
    public CoverType CoverType { get; init; }
    public string? RemoteUrl { get; init; }
    public string? Url { get; init; }
}
