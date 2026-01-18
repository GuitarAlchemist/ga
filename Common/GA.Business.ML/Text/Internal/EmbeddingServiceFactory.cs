namespace GA.Business.ML.Text.Internal;

using Abstractions;
using Configuration;
using HuggingFace;
using Ollama;
using Onnx;

using Microsoft.Extensions.Options;

public class EmbeddingServiceFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<EmbeddingServiceSettings> settings)
{
    public ITextEmbeddingService CreateService()
    {
        return settings.Value.ServiceType switch
        {
            EmbeddingServiceType.HuggingFace => new HuggingFaceEmbeddingService(
                httpClientFactory.CreateClient(),
                settings.Value.ApiKey,
                settings.Value.ModelName),

            EmbeddingServiceType.OnnxLocal => new OnnxEmbeddingService(
                settings.Value.ModelPath ??
                throw new ArgumentNullException(nameof(EmbeddingServiceSettings.ModelPath))),

            EmbeddingServiceType.Ollama => new OllamaEmbeddingService(
                httpClientFactory.CreateClient(),
                settings.Value.OllamaHost ?? "http://localhost:11434",
                settings.Value.ModelName ?? "nomic-embed-text"),

            _ => throw new ArgumentException($"Unsupported embedding service type: {settings.Value.ServiceType}")
        };
    }
}

