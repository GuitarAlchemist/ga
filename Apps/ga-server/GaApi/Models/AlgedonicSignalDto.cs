namespace GaApi.Models;

using System.Text.Json.Serialization;

/// <summary>
///     Represents an algedonic signal — a pain or pleasure event detected
///     from governance belief state transitions.
/// </summary>
public record AlgedonicSignalDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("signal")]
    public string Signal { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "active";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("node_id")]
    public string? NodeId { get; init; }
}
