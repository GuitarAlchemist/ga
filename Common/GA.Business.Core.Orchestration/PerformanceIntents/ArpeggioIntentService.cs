namespace GA.Business.Core.Orchestration.PerformanceIntents;

using System.Text;
using System.Text.Json;
using GA.Business.Core.Orchestration.Clients;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tracer for ga#589: constrains arpeggio-suggestion advice to a validated
/// <see cref="PerformanceIntent"/> instead of free-form LLM text.
/// <para>
/// Flow: build a prompt → have Ollama emit a <see cref="PerformanceIntent"/> via its native
/// JSON-schema structured outputs (<see cref="PerformanceIntentSchema.Json"/>) → validate through
/// the deterministic theory engine (<see cref="PerformanceIntentValidator"/>) → render the validated
/// intent, or refuse deterministically. An intent that fails validation never becomes a
/// plausible-looking free-text answer — the failure mode #567 documented.
/// </para>
/// </summary>
public sealed class ArpeggioIntentService(
    OllamaGenerateClient ollama,
    PerformanceIntentValidator validator,
    ILogger<ArpeggioIntentService> logger)
{
    /// <summary>
    /// Ask the model for arpeggio suggestions over <paramref name="chords"/> in <paramref name="key"/>,
    /// then validate and render. Talks to Ollama; use <see cref="InterpretModelJson"/> to exercise the
    /// deserialise → validate → render path without a live model.
    /// </summary>
    public async Task<ArpeggioAnswer> SuggestAsync(
        string[] chords, string key, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(chords, key);

        PerformanceIntent? intent;
        try
        {
            intent = await ollama.GenerateStructuredAsync<PerformanceIntent>(
                prompt, PerformanceIntentSchema.Node, temperature: 0.1f, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Arpeggio intent generation failed for [{Chords}] in {Key}", string.Join(" ", chords), key);
            return ArpeggioAnswer.CannotAnswer(["The model backend was unavailable or returned an unusable response."]);
        }

        return Interpret(intent);
    }

    /// <summary>
    /// Deserialise raw model output into a <see cref="PerformanceIntent"/> and run the full
    /// interpret path. This is the seam the regression tests drive with the exact #567 outputs.
    /// </summary>
    public ArpeggioAnswer InterpretModelJson(string modelJson)
    {
        PerformanceIntent? intent;
        try
        {
            intent = JsonSerializer.Deserialize<PerformanceIntent>(modelJson, PerformanceIntentSchema.SerializerOptions);
        }
        catch (JsonException)
        {
            return ArpeggioAnswer.CannotAnswer(["The model produced output that was not valid JSON."]);
        }

        return Interpret(intent);
    }

    /// <summary>Validate an already-materialised intent and render it, or refuse.</summary>
    public ArpeggioAnswer Interpret(PerformanceIntent? intent)
    {
        var validation = validator.Validate(intent);
        if (!validation.IsValid)
        {
            logger.LogDebug("Rejected arpeggio intent: {Problems}", string.Join("; ", validation.Problems));
            return ArpeggioAnswer.CannotAnswer(validation.Problems);
        }

        return new ArpeggioAnswer(true, Render(intent!), [], intent);
    }

    private static string BuildPrompt(string[] chords, string key)
    {
        var progression = string.Join(" ", chords ?? []);
        return
            $"You are a music-theory assistant. For the chord progression [{progression}] in the key of {key}, " +
            "suggest, for each chord, an arpeggio to play over it and the mode that fits.\n" +
            "Respond ONLY with a JSON object matching the provided schema. Every 'arpeggio' must be a real, " +
            "canonical chord symbol (e.g. 'Am7', not 'Amm7'), rooted on that chord. If a chord is borrowed or " +
            "secondary (not diatonic to the key), still report it — do not force it onto a diatonic mode.";
    }

    private static string Render(PerformanceIntent intent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Arpeggio suggestions in **{intent.Key}**:");
        sb.AppendLine();
        foreach (var s in intent.SuggestedArpeggios ?? [])
        {
            sb.AppendLine($"- **{s.Chord}** → play **{s.Arpeggio}** arpeggio over **{s.Mode}**");
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Result of an arpeggio-suggestion request. When <see cref="Answered"/> is <see langword="false"/>,
/// <see cref="Text"/> is a deterministic refusal that lists <see cref="Problems"/> — it never contains
/// unvalidated music-theory claims.
/// </summary>
public sealed record ArpeggioAnswer(bool Answered, string Text, IReadOnlyList<string> Problems, PerformanceIntent? Intent)
{
    public static ArpeggioAnswer CannotAnswer(IReadOnlyList<string> problems) => new(
        false,
        "I can't give validated arpeggio advice for this request. " + string.Join(" ", problems),
        problems,
        null);
}
