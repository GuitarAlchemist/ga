namespace GA.Business.ML.Search;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
///     Converts a natural-language query into a <see cref="StructuredQuery"/> by prompting an LLM
///     for structured extraction. This is the single text ↔ geometry bridge point — downstream
///     of here, everything is musical vectors.
/// </summary>
public interface IMusicalQueryExtractor
{
    Task<StructuredQuery> ExtractAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
///     LLM-backed implementation that uses the same <see cref="IChatClient"/> the agents inject.
///     Results are cached by SHA-256(query) — a warm cache effectively eliminates the extraction
///     cost for repeat queries in a session.
/// </summary>
public sealed class LlmMusicalQueryExtractor(
    IChatClient chatClient,
    IMemoryCache cache,
    ILogger<LlmMusicalQueryExtractor> logger) : IMusicalQueryExtractor
{
    private const string SystemPrompt = """
        You extract musical intent from user queries about guitar chord voicings. Respond with JSON only.

        Schema:
        {
          "chord": "<canonical chord symbol like Cmaj7, F#m7b5, or null>",
          "mode":  "<mode/scale name like Lydian, Dorian, or null>",
          "tags":  ["<style or technique lowercase tokens>"]
        }

        Rules:
        - "chord": prefer the root-quality form (e.g. "Cmaj7" not "Cmajor seventh"). Null if none mentioned.
        - "mode": null if the user didn't mention a mode/scale.
        - "tags": up to 6 lowercase keywords. Prefer style words (jazz, blues, rock, classical, folk, metal)
          and technique words (drop2, drop3, shell, quartal, barre, open, closed, rootless).
          Drop vague descriptors unless they match this vocabulary.
        - Return only the JSON object, no prose, no code fences.
        """;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<StructuredQuery> ExtractAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new StructuredQuery(null, null, null, null, null);

        var key = CacheKey(query);
        if (cache.TryGetValue<StructuredQuery>(key, out var cached) && cached is not null)
            return cached;

        StructuredQuery result;
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User,   query)
            };
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var text = response.Messages.LastOrDefault()?.Text ?? "";
            result = Parse(text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Musical query extraction failed for: {Query}", query);
            result = new StructuredQuery(null, null, null, null, null);
        }

        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });
        return result;
    }

    private static StructuredQuery Parse(string llmText)
    {
        var json = Unwrap(llmText);
        if (string.IsNullOrWhiteSpace(json)) return new StructuredQuery(null, null, null, null, null);

        try
        {
            var raw = JsonSerializer.Deserialize<RawExtraction>(json, JsonOpts);
            if (raw == null) return new StructuredQuery(null, null, null, null, null);

            int? root = null;
            int[]? pcs = null;
            if (!string.IsNullOrWhiteSpace(raw.Chord) &&
                ChordPitchClasses.TryParse(raw.Chord, out var r, out var pitches))
            {
                root = r;
                pcs = pitches;
            }

            var tags = raw.Tags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .ToList();

            return new StructuredQuery(
                ChordSymbol: string.IsNullOrWhiteSpace(raw.Chord) ? null : raw.Chord,
                RootPitchClass: root,
                PitchClasses: pcs,
                ModeName: string.IsNullOrWhiteSpace(raw.Mode) ? null : raw.Mode,
                Tags: tags);
        }
        catch (JsonException)
        {
            return new StructuredQuery(null, null, null, null, null);
        }
    }

    private static string Unwrap(string llmText)
    {
        // Brace-slicing is the most robust approach: LLMs wrap JSON in various ways
        // (```json, ```, bare prose then JSON, etc.). We just find the outermost braces
        // and trust Deserialize to reject malformed inner content.
        var t = llmText.Trim();
        var firstBrace = t.IndexOf('{');
        var lastBrace = t.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? t[firstBrace..(lastBrace + 1)]
            : string.Empty;
    }

    private static string CacheKey(string query)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(query));
        return "mqe:" + Convert.ToHexString(hash);
    }

    private sealed record RawExtraction
    {
        [JsonPropertyName("chord")] public string? Chord { get; init; }
        [JsonPropertyName("mode")]  public string? Mode { get; init; }
        [JsonPropertyName("tags")]  public List<string>? Tags { get; init; }
    }
}
