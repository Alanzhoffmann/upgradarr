namespace Upgradarr.Integrations.Models;

public record QualityModel
{
    public Quality? Quality { get; init; }
    public Revision? Revision { get; init; }
}

public record Quality
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Source { get; init; }
    public int Resolution { get; init; }
    public string? Modifier { get; init; }
}

public record Revision
{
    public int Version { get; init; }
    public int Real { get; init; }
    public bool IsRepack { get; init; }
}
