namespace GA.Business.Core.AI.Services.Embeddings;

using System.Text.Json.Serialization;

/// <summary>
/// Response model for Ollama embedding API
/// </summary>
internal class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}
