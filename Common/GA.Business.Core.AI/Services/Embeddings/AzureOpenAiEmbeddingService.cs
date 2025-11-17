namespace GA.Business.Core.AI.Services.Embeddings;

public class AzureOpenAiEmbeddingService(string endpoint, string apiKey, string deploymentName) : IEmbeddingService
{
    private readonly string _apiKey = apiKey;
    private readonly string _deploymentName = deploymentName;
    private readonly string _endpoint = endpoint;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Implementation for Azure OpenAI API
        throw new NotImplementedException();
    }
}
