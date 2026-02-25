namespace Huntarr.Net.Api.Models;

/// <summary>
/// Represents the type of item in the upgrade queue
/// </summary>
public enum ItemType
{
    /// <summary>
    /// No item type
    /// </summary>
    None = 0,

    /// <summary>
    /// A series/show
    /// </summary>
    Series = 1,

    /// <summary>
    /// A season within a series
    /// </summary>
    Season = 2,

    /// <summary>
    /// An episode within a season
    /// </summary>
    Episode = 3,

    /// <summary>
    /// A movie
    /// </summary>
    Movie = 4,
}
