using Microsoft.Extensions.Configuration;

namespace GaCLI;

public class ChatCompletionConfig
{
    public static ChatCompletionConfig FromSection(IConfigurationSection section)
    {
        return new ChatCompletionConfig(
            section["ModelId"]!,
            section["EndPoint"]!,
            section["ApiKey"]!);
    }
    
    public void Deconstruct(out string modelId, out string endPoint, out string apiKey)
    {
        modelId = ModelId;
        endPoint = EndPoint;
        apiKey = ApiKey;
    }

    public string ModelId { get; } = null!;
    public string EndPoint { get; } = null!;
    public string ApiKey { get; } = null!;

    public ChatCompletionConfig()
    {
    }

    public ChatCompletionConfig(string modelId, string endPoint, string apiKey)
    {
        ModelId = modelId;
        EndPoint = endPoint;
        ApiKey = apiKey;
    }
}