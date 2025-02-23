using Microsoft.Extensions.Options;
using OpenAI;

namespace GA.Data.MongoDB.Services.Embeddings;

public class OpenAiEmbeddingService(IOptions<OpenAiSettings> settings) : IEmbeddingService
{
    private readonly OpenAIClient _client = new(settings.Value.ApiKey);
    private readonly string _modelName = settings.Value.ModelName;

    public async Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        var embeddingClient = _client.GetEmbeddingClient(_modelName);
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        var floats = response.Value.ToFloats();
        return floats.ToArray().ToList();
    }
}