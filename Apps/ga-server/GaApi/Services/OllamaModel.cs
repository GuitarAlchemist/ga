namespace GaApi.Services;

using System.Text.Json.Serialization;

public class OllamaModel
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; set; }
}
