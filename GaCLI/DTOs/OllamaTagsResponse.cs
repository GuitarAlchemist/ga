namespace GaCLI.DTOs;

public class OllamaTagsResponse
{
    [JsonPropertyName("models")] public List<OllamaModel> Models { get; set; } = [];
}
