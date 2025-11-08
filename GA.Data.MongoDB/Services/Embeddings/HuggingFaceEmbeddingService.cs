namespace GA.Data.MongoDB.Services.Embeddings;

public class HuggingFaceEmbeddingService(
    HttpClient client,
    string apiKey,
    string model = "sentence-transformers/all-MiniLM-L6-v2")
    : IEmbeddingService
{
    private readonly string _apiKey = apiKey;
    private readonly HttpClient _client = client;
    private readonly string _model = model;

    public Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Implementation for Hugging Face API
        throw new NotImplementedException();
    }
}
