namespace GA.Data.MongoDB.Services.Embeddings;

using Microsoft.Extensions.Logging;

public class SimpleEmbeddingService(ILogger<SimpleEmbeddingService> logger) : IEmbeddingService
{
    public Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Simple implementation - creates a basic embedding based on text length and character values
        // This should be replaced with a proper embedding service in production
        var embedding = new List<float>();

        // Create a simple 128-dimension embedding
        for (var i = 0; i < 128; i++)
        {
            var value = i < text.Length ? text[i] / 255.0f : 0.0f;
            embedding.Add(value);
        }

        logger.LogDebug("Generated embedding for text of length {Length}", text.Length);
        return Task.FromResult(embedding);
    }
}
