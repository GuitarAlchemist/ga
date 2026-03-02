namespace GaApi.Services;

/// <summary>
///     Search result from semantic knowledge source.
/// </summary>
/// <param name="Content">The content to include in the LLM context.</param>
/// <param name="Score">The relevance score (0-1).</param>
public sealed record SemanticSearchResult(string Content, double Score);
