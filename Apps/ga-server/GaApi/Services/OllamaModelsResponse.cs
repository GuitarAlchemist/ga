namespace GaApi.Services;

using System.Text.Json.Serialization;

public class OllamaModelsResponse
{
    [JsonPropertyName("models")] public List<OllamaModel>? Models { get; set; }
}
