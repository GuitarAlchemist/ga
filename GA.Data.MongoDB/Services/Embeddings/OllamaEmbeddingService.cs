using System.Text;
using System.Text.Json;

namespace GA.Data.MongoDB.Services.Embeddings;

public class OllamaEmbeddingService(
    HttpClient client,
    string host = "http://localhost:11434",
    string modelName = "llama2")
    : IEmbeddingService
{
    private readonly string _host = host.TrimEnd('/');

    public async Task<List<float>> GenerateEmbeddingAsync(string text)
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

        var response = await client.PostAsync($"{_host}/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var responseJson = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseString);

        return responseJson?.Embedding ?? throw new InvalidOperationException("No embedding returned from Ollama");
    }
}