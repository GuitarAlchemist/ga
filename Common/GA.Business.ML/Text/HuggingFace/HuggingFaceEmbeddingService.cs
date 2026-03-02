namespace GA.Business.ML.Text.HuggingFace;

using Abstractions;

public class HuggingFaceEmbeddingService(
    HttpClient client,
    string apiKey,
    string model = "sentence-transformers/all-MiniLM-L6-v2")
    : ITextEmbeddingService
{
    private readonly string _apiKey = apiKey;
    private readonly HttpClient _client = client;
    private readonly string _model = model;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/pipeline/feature-extraction/{_model}");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        
        var payload = new { inputs = text };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // HF inference returns an array of floats directly for this pipeline, usually [[float_1, ..., float_n]] for single input, or a flat array.
        var embeddings = JsonSerializer.Deserialize<float[]>(responseContent);
        
        if (embeddings == null)
        {
            throw new InvalidOperationException("Failed to deserialize Hugging Face embedding response.");
        }
        
        return embeddings;
    }
}
