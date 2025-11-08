namespace GA.Data.MongoDB.Services.Embeddings;

using Microsoft.Extensions.Options;

public class EmbeddingServiceFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<EmbeddingServiceSettings> settings)
{
    public IEmbeddingService CreateService()
    {
        return settings.Value.ServiceType switch
        {
            EmbeddingServiceType.OpenAi => new OpenAiEmbeddingService(
                Options.Create(new OpenAiSettings
                {
                    ApiKey = settings.Value.ApiKey,
                    ModelName = settings.Value.ModelName ?? "text-embedding-ada-002"
                })),

            EmbeddingServiceType.HuggingFace => new HuggingFaceEmbeddingService(
                httpClientFactory.CreateClient(),
                settings.Value.ApiKey,
                settings.Value.ModelName),

            EmbeddingServiceType.AzureOpenAi => new AzureOpenAiEmbeddingService(
                settings.Value.Endpoint ?? throw new ArgumentNullException(nameof(EmbeddingServiceSettings.Endpoint)),
                settings.Value.ApiKey,
                settings.Value.DeploymentName ??
                throw new ArgumentNullException(nameof(EmbeddingServiceSettings.DeploymentName))),

            EmbeddingServiceType.OnnxLocal => new OnnxEmbeddingService(
                settings.Value.ModelPath ??
                throw new ArgumentNullException(nameof(EmbeddingServiceSettings.ModelPath))),

            EmbeddingServiceType.Ollama => new OllamaEmbeddingService(
                httpClientFactory.CreateClient(),
                settings.Value.OllamaHost ?? "http://localhost:11434",
                settings.Value.ModelName ?? "llama2"),

            _ => throw new ArgumentException($"Unsupported embedding service type: {settings.Value.ServiceType}")
        };
    }
}
