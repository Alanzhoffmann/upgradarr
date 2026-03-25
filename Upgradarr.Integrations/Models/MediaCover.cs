using Upgradarr.Integrations.Enums;

namespace Upgradarr.Integrations.Models;

public record MediaCover
{
    public CoverType CoverType { get; init; }
    public string? RemoteUrl { get; init; }
    public string? Url { get; init; }
}
