namespace GaCLI.DTOs;

public class OllamaModelDetails
{
    [JsonPropertyName("parent_model")]
    public string ParentModel { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("families")]
    public List<string> Families { get; set; } = [];

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = string.Empty;

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = string.Empty;
}