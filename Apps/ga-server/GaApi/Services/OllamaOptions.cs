namespace GaApi.Services;

using System.Text.Json.Serialization;

public class OllamaOptions
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("top_p")] public double TopP { get; set; } = 0.9;

    [JsonPropertyName("num_predict")] public int? NumPredict { get; set; }
}