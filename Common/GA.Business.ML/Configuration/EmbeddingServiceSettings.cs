namespace GA.Business.ML.Configuration;

public class EmbeddingServiceSettings
{
    public EmbeddingServiceType ServiceType { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? ModelName { get; set; }
    public string? DeploymentName { get; set; }
    public string? ModelPath { get; set; }
    public string? OllamaHost { get; set; } = "http://localhost:11434"; // Default Ollama endpoint
}
