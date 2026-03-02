namespace GaApi.Hubs;

/// <summary>
///     Semantic search result payload for hub consumers.
/// </summary>
public sealed class SemanticSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}