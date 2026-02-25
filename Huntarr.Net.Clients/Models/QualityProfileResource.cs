namespace Huntarr.Net.Clients.Models;

public record QualityProfileResource
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public bool UpgradeAllowed { get; init; }
    public int Cutoff { get; init; }
    public IEnumerable<QualityProfileQualityItemResource>? Items { get; init; }
    public int MinFormatScore { get; init; }
    public int CutoffFormatScore { get; init; }
    public int MinUpgradeFormatScore { get; init; }
    public IEnumerable<ProfileFormatItemResource>? FormatItems { get; init; }
}

public record QualityProfileQualityItemResource
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public Quality? Quality { get; init; }
    public IEnumerable<QualityProfileQualityItemResource>? Items { get; init; }
    public bool Allowed { get; init; }
}

public record ProfileFormatItemResource
{
    public int Format { get; init; }
    public string? Name { get; init; }
    public int Score { get; init; }
}
