namespace Upgradarr.Domain.ValueObjects;

public record QueueItemScore
{
    public required int ItemId { get; init; }
    public int CustomFormatScore { get; set; }
}
