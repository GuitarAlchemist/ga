namespace GA.Business.Core.AI.Services.Embeddings;

public class HuggingFaceEmbeddingService(
    HttpClient client,
    string apiKey,
    string model = "sentence-transformers/all-MiniLM-L6-v2")
    : IEmbeddingService
{
    private readonly string _apiKey = apiKey;
    private readonly HttpClient _client = client;
    private readonly string _model = model;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Implementation for Hugging Face API
        throw new NotImplementedException();
    }
}
