namespace GaApi.Services;

/// <summary>
///     Abstraction over semantic knowledge retrieval to keep chat orchestration testable.
/// </summary>
public interface ISemanticKnowledgeSource
{
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}
