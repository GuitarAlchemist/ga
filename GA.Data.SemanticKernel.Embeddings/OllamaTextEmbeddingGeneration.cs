namespace GA.Data.SemanticKernel.Embeddings;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

[Experimental("SKEXP0001")]
public class OllamaTextEmbeddingGeneration(HttpClient httpClient, string modelName) : ITextEmbeddingGenerationService
{
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> texts,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<ReadOnlyMemory<float>>();

        foreach (var text in texts)
        {
            var request = new
            {
                model = modelName,
                prompt = text
            };

            var response = await httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                cancellationToken);

            if (result?.Embedding != null)
            {
                embeddings.Add(new ReadOnlyMemory<float>(result.Embedding));
            }
        }

        return embeddings;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
}
