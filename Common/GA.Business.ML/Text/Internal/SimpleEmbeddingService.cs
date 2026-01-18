namespace GA.Business.ML.Text.Internal;

using Abstractions;

using Microsoft.Extensions.Logging;

public class SimpleEmbeddingService(ILogger<SimpleEmbeddingService> logger) : ITextEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Simple implementation - creates a basic embedding based on text length and character values
        // This should be replaced with a proper embedding service in production
        var embedding = new float[128];

        // Create a simple 128-dimension embedding
        for (var i = 0; i < 128; i++)
        {
            embedding[i] = i < text.Length ? text[i] / 255.0f : 0.0f;
        }

        logger.LogDebug("Generated embedding for text of length {Length}", text.Length);
        return Task.FromResult(embedding);
    }
}
