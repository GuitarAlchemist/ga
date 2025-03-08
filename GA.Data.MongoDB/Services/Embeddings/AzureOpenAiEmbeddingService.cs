namespace GA.Data.MongoDB.Services.Embeddings;

public class AzureOpenAiEmbeddingService(string endpoint, string apiKey, string deploymentName) : IEmbeddingService
{
    private readonly string _endpoint = endpoint;
    private readonly string _apiKey = apiKey;
    private readonly string _deploymentName = deploymentName;

    public async Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Implementation for Azure OpenAI API
        throw new NotImplementedException();
    }
}