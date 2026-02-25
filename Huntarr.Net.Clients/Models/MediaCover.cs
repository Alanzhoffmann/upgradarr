using Huntarr.Net.Clients.Enums;

namespace Huntarr.Net.Clients.Models;

public record MediaCover
{
    public CoverType CoverType { get; init; }
    public string? RemoteUrl { get; init; }
    public string? Url { get; init; }
}
