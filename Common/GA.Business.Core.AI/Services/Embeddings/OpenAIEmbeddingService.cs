namespace GA.Business.Core.AI.Services.Embeddings;

using Microsoft.Extensions.Options;
using OpenAI;

public class OpenAiEmbeddingService(IOptions<OpenAiSettings> settings) : IEmbeddingService
{
    private readonly OpenAIClient _client = new(settings.Value.ApiKey);
    private readonly string _modelName = settings.Value.ModelName;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddingClient = _client.GetEmbeddingClient(_modelName);
        var response = await embeddingClient.GenerateEmbeddingAsync(text, options: null, cancellationToken);
        var floats = response.Value.ToFloats();
        return floats.ToArray();
    }
}
