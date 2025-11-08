namespace GA.Data.MongoDB.Services.Embeddings;

public interface IEmbeddingService
{
    Task<List<float>> GenerateEmbeddingAsync(string text);
}
