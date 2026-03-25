namespace Upgradarr.Contracts;

public class UpgradeStateDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string ItemType { get; init; } = "";
    public string SearchState { get; init; } = "";
    public int QueuePosition { get; init; }
}
