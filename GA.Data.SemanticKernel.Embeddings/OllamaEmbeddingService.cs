namespace GA.Data.SemanticKernel.Embeddings;

using Microsoft.SemanticKernel.Embeddings;

public class OllamaEmbeddingService(ITextEmbeddingGenerationService embeddingService) : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _embeddingService = embeddingService;
    
    public int EmbeddingDimension => 384; // For nomic-embed-text

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
            new[] { text }, 
            cancellationToken: cancellationToken);
            
        return embeddings[0].ToArray();
    }
}