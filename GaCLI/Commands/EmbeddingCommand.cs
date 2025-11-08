namespace GaCLI.Commands;

using GA.Business.Intelligence.SemanticIndexing;

public class EmbeddingCommand
{
    private readonly SemanticSearchService _searchService;

    public EmbeddingCommand(SemanticSearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task ExecuteAsync(string text)
    {
        // Stub implementation for embedding command
        var results = await _searchService.SearchAsync(text, 10);
        // Process results...
    }
}
