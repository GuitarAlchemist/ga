namespace GaApi.Constants;

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
    public static string MusicFloorItems(int floorNumber)
    {
        return $"music:floor:{floorNumber}:items";
    }

    // Chord Cache Keys
    /// <summary>
    ///     Get cache key for chord search results
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Result limit</param>
    /// <returns>Cache key for chord search</returns>
    public static string ChordSearch(string query, int limit)
    {
        return $"chords_search_{query}_{limit}";
    }

    // Grothendieck Cache Keys
    /// <summary>
    ///     Get cache key for fretboard shapes
    /// </summary>
    /// <param name="tuningId">Tuning identifier</param>
    /// <param name="pitchClasses">Pitch class set</param>
    /// <param name="hashCode">Request hash code</param>
    /// <returns>Cache key for shapes</returns>
    public static string FretboardShapes(string tuningId, string pitchClasses, int hashCode)
    {
        return $"shapes_{tuningId}_{pitchClasses}_{hashCode}";
    }

    /// <summary>
    ///     Get cache key for heat map
    /// </summary>
    /// <param name="currentShapeId">Current shape identifier</param>
    /// <param name="hashCode">Request hash code</param>
    /// <returns>Cache key for heat map</returns>
    public static string HeatMap(string currentShapeId, int hashCode)
    {
        return $"heatmap_{currentShapeId}_{hashCode}";
    }

    // Contextual Chord Cache Keys
    /// <summary>
    ///     Get cache key for contextual chords by key
    /// </summary>
    /// <param name="key">Musical key</param>
    /// <param name="extension">Chord extension</param>
    /// <param name="stackingType">Stacking type</param>
    /// <param name="onlyNaturallyOccurring">Only naturally occurring flag</param>
    /// <param name="includeBorrowedChords">Include borrowed chords flag</param>
    /// <param name="includeSecondaryDominants">Include secondary dominants flag</param>
    /// <param name="includeSecondaryTwoFive">Include secondary II-V flag</param>
    /// <param name="minCommonality">Minimum commonality</param>
    /// <param name="limit">Result limit</param>
    /// <returns>Cache key for contextual chords</returns>
    public static string ContextualChordsByKey(
        string key,
        string extension,
        string stackingType,
        bool onlyNaturallyOccurring,
        bool includeBorrowedChords,
        bool includeSecondaryDominants,
        bool includeSecondaryTwoFive,
        double minCommonality,
        int limit)
    {
        return
            $"key_{key}_ext:{extension}_stack:{stackingType}_nat:{onlyNaturallyOccurring}_bor:{includeBorrowedChords}_sec:{includeSecondaryDominants}_ii-v:{includeSecondaryTwoFive}_min:{minCommonality:F2}_lim:{limit}";
    }

    /// <summary>
    ///     Get cache key for contextual chords by scale
    /// </summary>
    public static string ContextualChordsByScale(
        string scale,
        string extension,
        string stackingType,
        bool onlyNaturallyOccurring,
        bool includeBorrowedChords,
        bool includeSecondaryDominants,
        bool includeSecondaryTwoFive,
        double minCommonality,
        int limit)
    {
        return
            $"scale_{scale}_ext:{extension}_stack:{stackingType}_nat:{onlyNaturallyOccurring}_bor:{includeBorrowedChords}_sec:{includeSecondaryDominants}_ii-v:{includeSecondaryTwoFive}_min:{minCommonality:F2}_lim:{limit}";
    }

    /// <summary>
    ///     Get cache key for contextual chords by mode
    /// </summary>
    public static string ContextualChordsByMode(
        string mode,
        string extension,
        string stackingType,
        bool onlyNaturallyOccurring,
        bool includeBorrowedChords,
        bool includeSecondaryDominants,
        bool includeSecondaryTwoFive,
        double minCommonality,
        int limit)
    {
        return
            $"mode_{mode}_ext:{extension}_stack:{stackingType}_nat:{onlyNaturallyOccurring}_bor:{includeBorrowedChords}_sec:{includeSecondaryDominants}_ii-v:{includeSecondaryTwoFive}_min:{minCommonality:F2}_lim:{limit}";
    }

    // Web Content Cache Keys (for GA.Business.Core.Web)
    /// <summary>
    ///     Get cache key for web page content
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <param name="extractMainContent">Extract main content flag</param>
    /// <returns>Cache key for web page</returns>
    public static string WebPage(string url, bool extractMainContent)
    {
        return $"webpage:{url}:{extractMainContent}";
    }

    /// <summary>
    ///     Get cache key for web elements
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <param name="cssSelector">CSS selector</param>
    /// <returns>Cache key for elements</returns>
    public static string WebElements(string url, string cssSelector)
    {
        return $"elements:{url}:{cssSelector}";
    }

    /// <summary>
    ///     Get cache key for RSS feed
    /// </summary>
    /// <param name="url">Feed URL</param>
    /// <param name="maxItems">Maximum items</param>
    /// <returns>Cache key for feed</returns>
    public static string RssFeed(string url, int maxItems)
    {
        return $"feed:{url}:{maxItems}";
    }

    /// <summary>
    ///     Get cache key for DuckDuckGo search
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum results</param>
    /// <returns>Cache key for DuckDuckGo search</returns>
    public static string DuckDuckGoSearch(string query, int maxResults)
    {
        return $"ddg:{query}:{maxResults}";
    }

    /// <summary>
    ///     Get cache key for Wikipedia search
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum results</param>
    /// <returns>Cache key for Wikipedia search</returns>
    public static string WikipediaSearch(string query, int maxResults)
    {
        return $"wiki:{query}:{maxResults}";
    }

    // Cache Duration Constants
    public static class Durations
    {
        public static readonly TimeSpan MusicData = TimeSpan.FromHours(1);
        public static readonly TimeSpan ChordSearch = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan FretboardShapes = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan HeatMap = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan WebPage = TimeSpan.FromHours(2);
        public static readonly TimeSpan RssFeed = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DuckDuckGoSearch = TimeSpan.FromHours(6);
        public static readonly TimeSpan WikipediaSearch = TimeSpan.FromHours(24);
        public static readonly TimeSpan WikipediaSummary = TimeSpan.FromDays(7);
    }
}
