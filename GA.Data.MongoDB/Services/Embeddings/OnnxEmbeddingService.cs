namespace GA.Data.MongoDB.Services.Embeddings;

public class OnnxEmbeddingService(string modelPath) : IEmbeddingService
{
    private readonly string _modelPath = modelPath;

    public async Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Implementation for local ONNX model
        throw new NotImplementedException();
    }
}