namespace GA.Data.SemanticKernel.Embeddings;

public class EmbeddingConfig
{
    public string ModelName { get; set; } = "nomic-embed-text";
    public string Endpoint { get; set; } = "http://localhost:11434";
}
