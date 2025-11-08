namespace GaApi.Services;

/// <summary>
///     Default implementation that forwards to the core SemanticSearchService.
/// </summary>
public sealed class SemanticKnowledgeSource(SemanticSearchService semanticSearchService)
    : ISemanticKnowledgeSource
{
    public async Task<IReadOnlyList<SemanticSearchService.SearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = await semanticSearchService.SearchAsync(query, limit);

        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }
}
