namespace GaApi.Services;

using System.Text.Json.Serialization;

public class OllamaChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")] public bool Stream { get; set; } = true;

    [JsonPropertyName("options")] public OllamaOptions? Options { get; set; }
}
