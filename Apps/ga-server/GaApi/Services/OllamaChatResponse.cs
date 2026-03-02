namespace GaApi.Services;

using System.Text.Json.Serialization;

public class OllamaChatResponse
{
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonPropertyName("message")] public ChatMessage? Message { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }

    [JsonPropertyName("total_duration")] public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")] public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")] public int? EvalCount { get; set; }
}
