namespace GA.Business.Core.Orchestration.PerformanceIntents;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A typed "performance intent" for arpeggio-over-progression advice, emitted by the
/// LLM as structured output and validated by the deterministic theory engine before
/// anything reaches the user. See <see cref="PerformanceIntentValidator"/> and the
/// contract at <c>docs/contracts/performance-intent.schema.json</c>.
/// <para>
/// The LLM is a probabilistic sampler proposing this shape; the theory engine owns the
/// truth (tracer for the #567 bug class — <c>Amm7</c> concatenation, key-blind degree
/// mapping for borrowed chords). Fields are nullable because the model can omit them;
/// the validator rejects incomplete intents rather than the deserialiser throwing.
/// </para>
/// </summary>
public sealed record PerformanceIntent
{
    /// <summary>The chord the advice is primarily about, e.g. <c>"Am"</c>.</summary>
    [JsonPropertyName("chord")] public string? Chord { get; init; }

    /// <summary>The tonal centre the suggestions are framed in, e.g. <c>"C major"</c>.</summary>
    [JsonPropertyName("key")] public string? Key { get; init; }

    /// <summary>Scale-degree analysis, one entry per chord considered.</summary>
    [JsonPropertyName("degrees")] public IReadOnlyList<DegreeMapping>? Degrees { get; init; }

    /// <summary>Arpeggio + mode suggestions, one entry per chord considered.</summary>
    [JsonPropertyName("suggested_arpeggios")] public IReadOnlyList<ArpeggioSuggestion>? SuggestedArpeggios { get; init; }
}

/// <summary>Maps a chord to its Roman-numeral scale degree within <see cref="PerformanceIntent.Key"/>.</summary>
public sealed record DegreeMapping
{
    [JsonPropertyName("chord")] public string? Chord { get; init; }
    [JsonPropertyName("roman")] public string? Roman { get; init; }
}

/// <summary>A single arpeggio suggestion: the source chord, the canonical arpeggio symbol to play, and the mode.</summary>
public sealed record ArpeggioSuggestion
{
    [JsonPropertyName("chord")] public string? Chord { get; init; }

    /// <summary>Canonical chord symbol of the arpeggio to play over <see cref="Chord"/> (e.g. <c>"Am7"</c>).</summary>
    [JsonPropertyName("arpeggio")] public string? Arpeggio { get; init; }

    [JsonPropertyName("mode")] public string? Mode { get; init; }
}

/// <summary>
/// The JSON Schema handed to Ollama's native structured-output <c>format</c> field, plus the
/// deserialisation options. Kept as a string constant so it is the single source of truth,
/// mirrored verbatim to <c>docs/contracts/performance-intent.schema.json</c>.
/// </summary>
public static class PerformanceIntentSchema
{
    /// <summary>
    /// JSON Schema for <see cref="PerformanceIntent"/>. Enums are drawn from the repo's real
    /// diatonic + common-jazz mode vocabulary (the same mode strings used by ImprovisationSkill
    /// and the arpeggio MCP tool). Chord/arpeggio symbols are constrained by a root-note pattern
    /// only — the theory engine validates the full symbol afterwards, since a regex cannot know
    /// that <c>Amm7</c> is not a real chord.
    /// </summary>
    public const string Json =
        """
        {
          "type": "object",
          "properties": {
            "chord": { "type": "string", "pattern": "^[A-G][#b]?.*$" },
            "key":   { "type": "string", "pattern": "^[A-G][#b]?\\s+(major|minor)$" },
            "degrees": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "chord": { "type": "string", "pattern": "^[A-G][#b]?.*$" },
                  "roman": { "type": "string" }
                },
                "required": ["chord", "roman"]
              }
            },
            "suggested_arpeggios": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "chord":    { "type": "string", "pattern": "^[A-G][#b]?.*$" },
                  "arpeggio": { "type": "string", "pattern": "^[A-G][#b]?.*$" },
                  "mode": {
                    "type": "string",
                    "enum": [
                      "Ionian (major)", "Dorian", "Phrygian", "Lydian", "Mixolydian",
                      "Aeolian (minor)", "Locrian", "Melodic Minor", "Lydian Dominant",
                      "Mixolydian b6", "Altered (Super Locrian)", "Phrygian Dominant",
                      "Half-Whole Diminished", "Whole-Half Diminished", "Whole Tone",
                      "Lydian Augmented", "Locrian #2", "Major Pentatonic", "Minor Pentatonic"
                    ]
                  }
                },
                "required": ["chord", "arpeggio", "mode"]
              }
            }
          },
          "required": ["chord", "key", "suggested_arpeggios"]
        }
        """;

    /// <summary>The schema parsed as an object graph, ready to hand to <c>OllamaGenerateClient.GenerateStructuredAsync</c>.</summary>
    public static object Node { get; } =
        JsonSerializer.Deserialize<JsonElement>(Json);

    /// <summary>Case-insensitive options matching the snake_case wire shape via <see cref="JsonPropertyNameAttribute"/>.</summary>
    public static JsonSerializerOptions SerializerOptions { get; } =
        new() { PropertyNameCaseInsensitive = true };
}
