using Microsoft.Extensions.Configuration;

namespace GaCLI;

public class ChatCompletionConfig
{
    public static ChatCompletionConfig FromSection(IConfigurationSection section)
    {
        return new ChatCompletionConfig(
            section["ModelId"]!,
            section["ApiKey"]!);
    }
    
    public void Deconstruct(out string modelId, out string apiKey)
    {
        modelId = ModelId;
        apiKey = ApiKey;
    }

    public string ModelId { get; } = null!;
    public string ApiKey { get; } = null!;

    public ChatCompletionConfig()
    {
    }

    public ChatCompletionConfig(string modelId, string apiKey)
    {
        ModelId = modelId;
        ApiKey = apiKey;
    }
}