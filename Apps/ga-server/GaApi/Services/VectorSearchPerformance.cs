namespace GaApi.Services;

/// <summary>
///     Performance characteristics of a vector search strategy
/// </summary>
public record VectorSearchPerformance(
    TimeSpan ExpectedSearchTime,
    long MemoryUsageMb,
    bool RequiresGpu,
    bool RequiresNetwork);