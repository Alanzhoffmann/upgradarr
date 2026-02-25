namespace Huntarr.Net.Clients.Models;

public class QualityModel
{
    public Quality? Quality { get; set; }
    public Revision? Revision { get; set; }
}

public class Quality
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
    public int Resolution { get; set; }
    public string? Modifier { get; set; }
}

public class Revision
{
    public int Version { get; set; }
    public int Real { get; set; }
    public bool IsRepack { get; set; }
}
