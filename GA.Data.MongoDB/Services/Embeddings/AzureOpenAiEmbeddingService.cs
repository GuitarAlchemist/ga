namespace GA.Data.MongoDB.Services.Embeddings;

public class AzureOpenAiEmbeddingService(string endpoint, string apiKey, string deploymentName) : IEmbeddingService
{
    private readonly string _apiKey = apiKey;
    private readonly string _deploymentName = deploymentName;
    private readonly string _endpoint = endpoint;

    public Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Implementation for Azure OpenAI API
        throw new NotImplementedException();
    }
}
