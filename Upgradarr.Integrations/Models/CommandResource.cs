namespace Upgradarr.Integrations.Models;

public record CommandResource
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? CommandName { get; init; }
    public string? Message { get; init; }
    public string? Priority { get; init; }
    public string? Status { get; init; }
    public string? Result { get; init; }
    public DateTime Queued { get; init; }
    public DateTime? Started { get; init; }
    public DateTime? Ended { get; init; }
    public string? Exception { get; init; }
    public string? Trigger { get; init; }
    public string? ClientUserAgent { get; init; }
    public DateTime? StateChangeTime { get; init; }
    public bool SendUpdatesToClient { get; init; }
    public bool UpdateScheduledTask { get; init; }
    public DateTime? LastExecutionTime { get; init; }
}
