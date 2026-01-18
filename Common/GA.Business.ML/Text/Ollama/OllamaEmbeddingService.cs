namespace GA.Business.ML.Text.Ollama;

using Abstractions;

using System.Text;
using System.Text.Json;

public class OllamaEmbeddingService(
    HttpClient client,
    string host = "http://localhost:11434",
    string modelName = "llama2")
    : ITextEmbeddingService
{
    private readonly string _host = host.TrimEnd('/');

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = modelName,
            prompt = text
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{_host}/api/embeddings", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseJson = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseString);

        var embedding = responseJson?.Embedding ?? throw new InvalidOperationException("No embedding returned from Ollama");
        return embedding;
    }
}
