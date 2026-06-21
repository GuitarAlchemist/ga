namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Notation;

/// <summary>
/// Builds prompts for the LLM that are strictly grounded in retrieved data.
/// Sanitizes user input to prevent prompt injection.
/// </summary>
public class GroundedPromptBuilder
{
    private const int MaxQueryLength = 500;

    private static readonly Regex InjectionPattern = new(
        @"(SYSTEM\s*:|USER\s*:|ASSISTANT\s*:|\nHuman\s*:|\nAssistant\s*:|###\s*\n?|```)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string SanitizeQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var normalized = raw.Normalize(System.Text.NormalizationForm.FormKD);
        var sanitized = InjectionPattern.Replace(normalized, string.Empty);
        sanitized = Regex.Replace(sanitized, @"\s{3,}", " ").Trim();
        if (sanitized.Length > MaxQueryLength)
            sanitized = sanitized[..MaxQueryLength] + "…";
        return sanitized;
    }

    private static string SanitizeField(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? string.Empty
            : InjectionPattern.Replace(raw.Normalize(System.Text.NormalizationForm.FormKD), string.Empty).Trim();

    public static string Build(string userQuery, IReadOnlyList<CandidateVoicing> candidates)
    {
        var safeQuery = SanitizeQuery(userQuery);
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM: You are the Guitar Alchemist Assistant, an expert in harmonic geometry.");
        // Out-of-scope gate (2026-05-31). This RAG narrator is the production
        // responder for queries that fall through routing (Chatbot:Mode=full,
        // fallback disabled) — so a non-music query like "what's the weather"
        // reaches here with an empty manifest and the small model otherwise
        // answers it off-topic. The guardrail is deliberately scoped to
        // CLEARLY-unrelated queries and explicitly permits in-scope music
        // questions that happen to have no manifest data, so it declines
        // "what's the weather" without false-declining "what is the Dorian mode".
        sb.AppendLine(
            "SCOPE: You only assist with guitar, music theory, chords, scales, voicings, " +
            "progressions, and related musical topics. If the user's request is clearly " +
            "unrelated to music or guitar, do not attempt to answer it: politely decline in " +
            "one sentence and state what you can help with instead. For any music-related " +
            "question, proceed normally — answer from the manifest when it has data, and " +
            "otherwise from general music knowledge.");

        var q = safeQuery.ToLowerInvariant();
        if (q.Contains("jazz") || q.Contains("fusion") || q.Contains("neo-soul") || q.Contains("substitution") || q.Contains("shell") || q.Contains("extension"))
            sb.AppendLine("PERSONA: Act as a Jazz Harmony Professor. Focus on voice leading, extensions (9ths, 13ths), and harmonic function. Use terms like 'ii-V-I', 'tritone sub', and 'guide tones'.");
        else if (q.Contains("metal") || q.Contains("djent") || q.Contains("heavy") || q.Contains("power chord") || q.Contains("drop"))
            sb.AppendLine("PERSONA: Act as a Modern Metal Producer. Focus on aggression, gain staging, palm-muting mechanics, and low-end clarity.");
        else if (q.Contains("beginner") || q.Contains("easy") || q.Contains("simple") || q.Contains("how to"))
            sb.AppendLine("PERSONA: Act as a Friendly Guitar Teacher. Use simple terms and analogies.");
        else if (q.Contains("why") || q.Contains("explain") || q.Contains("function") || q.Contains("theoretical") || q.Contains("analyze") || q.Contains("distance") || q.Contains("compare"))
            sb.AppendLine("PERSONA: Act as a Harmonic Scientist. Analyze interval structure, resonance, and geometric relationships.");

        sb.AppendLine("CONCEPT MAP (Translate math to music):");
        sb.AppendLine("- 'Spectral Centroid/Position': Where the chord sits on the Circle of Fifths.");
        sb.AppendLine("- 'Geodesic Distance': Musical closeness — closer chords pull more naturally.");
        sb.AppendLine("- 'Spectral Velocity': Voice-leading effort.");
        sb.AppendLine();
        sb.AppendLine("STRICT CONSTRAINT: Only discuss the specific chord voicings in the manifest below.");
        sb.AppendLine("STRICT CONSTRAINT: If a chord is not in the manifest, do NOT mention it.");
        sb.AppendLine("STRICT CONSTRAINT: If you cannot answer using ONLY the manifest, state that clearly.");
        sb.AppendLine(PlayableNotationFormatter.PromptGuidance);
        sb.AppendLine();

        sb.AppendLine("### CHORD MANIFEST (GROUND TRUTH) ###");
        if (candidates.Count == 0)
        {
            sb.AppendLine("[NO DATA FOUND IN DATABASE]");
        }
        else
        {
            foreach (var c in candidates)
            {
                sb.AppendLine($"- ID: {c.Id}");
                sb.AppendLine($"  Name: {SanitizeField(c.DisplayName)}");
                sb.AppendLine($"  Fingering: {SanitizeField(c.Shape)}");
                sb.AppendLine($"  Theory: {SanitizeField(c.ExplanationFacts.Summary)}");
                sb.AppendLine($"  Modal Colors: {string.Join(", ", c.ExplanationFacts.Tags.Select(SanitizeField).Where(t => t.StartsWith("Flavor:")))}");
                if (c.ExplanationFacts.SpectralCentroid.HasValue)
                    sb.AppendLine($"  Spectral Position: {c.ExplanationFacts.SpectralCentroid.Value:F2} radians on Fifth Cycle");
                sb.AppendLine();
            }
        }

        sb.AppendLine("### USER QUERY ###");
        sb.AppendLine(safeQuery);
        sb.AppendLine();
        sb.AppendLine("### NARRATOR INSTRUCTION ###");
        sb.AppendLine("Explain why these specific voicings work for the user's request.");
        sb.AppendLine("When you mention a manifest fingering, include its matching fenced `vextab` block.");
        sb.AppendLine("Answer in a concise, helpful tone.");

        return sb.ToString();
    }
}
