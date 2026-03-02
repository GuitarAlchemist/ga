namespace GaApi.Services;

/// <summary>
///     Vector search statistics
/// </summary>
public record VectorSearchStats(
    long TotalChords,
    long MemoryUsageMb,
    TimeSpan AverageSearchTime,
    long TotalSearches);
