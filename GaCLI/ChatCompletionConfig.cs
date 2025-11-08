namespace GaCLI;

using Microsoft.Extensions.Configuration;

public class ChatCompletionConfig
{
    public ChatCompletionConfig()
    {
    }

    public ChatCompletionConfig(string modelId, string apiKey)
    {
        ModelId = modelId;
        ApiKey = apiKey;
    }

    public string ModelId { get; } = null!;
    public string ApiKey { get; } = null!;

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
}
