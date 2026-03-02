namespace GA.MusicTheory.Service.Constants;

/// <summary>
///     Centralized cache key definitions for consistent caching across the application
/// </summary>
public static class CacheKeys
{
    // Music Data Cache Keys
    public const string MusicSetClasses = "music:set-classes";
    public const string MusicForteNumbers = "music:forte-numbers";

    /// <summary>
    ///     Get cache key for music floor items
    /// </summary>
    /// <param name="floorNumber">Floor number (0-5)</param>
    /// <returns>Cache key for the specified floor</returns>
    public static string MusicFloorItems(int floorNumber) => $"music:floor:{floorNumber}:items";

    // Chord Cache Keys
    /// <summary>
    ///     Get cache key for chord search results
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Result limit</param>
    /// <returns>Cache key for chord search</returns>
    public static string ChordSearch(string query, int limit) => $"chords_search_{query}_{limit}";

    // Cache Duration Constants
    public static class Durations
    {
        public static readonly TimeSpan MusicData = TimeSpan.FromHours(1);
        public static readonly TimeSpan ChordSearch = TimeSpan.FromMinutes(5);
    }
}
