namespace Upgradarr.Domain.Enums;

public enum UpgradeActionResult
{
    /// <summary>
    /// Item was skipped because it's not ready, has ongoing downloads, or no upgrade was found.
    /// </summary>
    Skipped,

    /// <summary>
    /// A search was successfully executed for the item.
    /// </summary>
    Searched,

    /// <summary>
    /// Item is no longer monitored or valid, and should be removed from the queue.
    /// </summary>
    Removed,
}
