namespace GaCLI.Commands;

using GA.Data.MongoDB.Services.Embeddings;

public class EmbeddingCommand(IEmbeddingService embeddingService)
{
    public async Task ExecuteAsync(string text)
    {
        var embeddings = await embeddingService.GenerateEmbeddingAsync(text);
        // Process embeddings...
    }
}