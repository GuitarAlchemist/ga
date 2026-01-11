namespace GaCLI.Commands;

using GA.Business.Intelligence.SemanticIndexing;

public class EmbeddingCommand(SemanticSearchService searchService)
{
    public async Task ExecuteAsync(string text)
    {
        // Stub implementation for embedding command
        var results = await searchService.SearchAsync(text, 10);
        // Process results...
    }
}
