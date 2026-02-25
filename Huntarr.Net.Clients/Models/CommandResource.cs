namespace Huntarr.Net.Clients.Models;

public class CommandResource
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? CommandName { get; set; }
    public string? Message { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public DateTime Queued { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Ended { get; set; }
    public string? Exception { get; set; }
    public string? Trigger { get; set; }
    public string? ClientUserAgent { get; set; }
    public DateTime? StateChangeTime { get; set; }
    public bool SendUpdatesToClient { get; set; }
    public bool UpdateScheduledTask { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}
