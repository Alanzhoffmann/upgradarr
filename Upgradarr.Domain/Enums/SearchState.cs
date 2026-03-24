namespace Upgradarr.Domain.Enums;

/// <summary>
/// Represents the search state of an upgrade queue item
/// </summary>
public enum SearchState
{
    /// <summary>
    /// Item is pending and ready to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Item has been searched
    /// </summary>
    Searched = 1,

    /// <summary>
    /// Item search failed
    /// </summary>
    Failed = 2,
}
