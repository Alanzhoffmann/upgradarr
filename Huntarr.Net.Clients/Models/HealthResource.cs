namespace Huntarr.Net.Clients.Models;

public class HealthResource
{
    public int Id { get; set; }
    public string? Source { get; set; }
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? WikiUrl { get; set; }
}
