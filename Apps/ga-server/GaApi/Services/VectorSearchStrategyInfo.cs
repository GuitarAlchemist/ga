namespace GaApi.Services;

/// <summary>
///     Information about the current vector search strategy
/// </summary>
public record VectorSearchStrategyInfo(
    string Name,
    bool IsAvailable,
    VectorSearchPerformance? Performance,
    VectorSearchStats? Stats);