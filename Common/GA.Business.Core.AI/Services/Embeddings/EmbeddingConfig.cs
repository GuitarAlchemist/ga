namespace GA.Business.Core.AI.Services.Embeddings;

public class EmbeddingConfig
{
    public string ModelName { get; set; } = "nomic-embed-text";
    public string Endpoint { get; set; } = "http://localhost:11434";
}
