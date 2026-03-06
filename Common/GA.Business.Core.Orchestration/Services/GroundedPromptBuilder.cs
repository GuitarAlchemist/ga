namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Builds prompts for the LLM that are strictly grounded in retrieved data.
/// Sanitizes user input to prevent prompt injection.
/// </summary>
public class GroundedPromptBuilder
{
    private const int MaxQueryLength = 500;

    private static readonly Regex InjectionPattern = new(
        @"(SYSTEM\s*:|USER\s*:|ASSISTANT\s*:|###\s+\w|```)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string SanitizeQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var sanitized = InjectionPattern.Replace(raw, string.Empty);
        sanitized = Regex.Replace(sanitized, @"\s{3,}", " ").Trim();
        if (sanitized.Length > MaxQueryLength)
            sanitized = sanitized[..MaxQueryLength] + "…";
        return sanitized;
    }

    public string Build(string userQuery, IReadOnlyList<CandidateVoicing> candidates)
    {
        var safeQuery = SanitizeQuery(userQuery);
        var sb = new StringBuilder();

        sb.AppendLine("SYSTEM: You are the Guitar Alchemist Assistant, an expert in harmonic geometry.");

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
                sb.AppendLine($"  Name: {c.DisplayName}");
                sb.AppendLine($"  Fingering: {c.Shape}");
                sb.AppendLine($"  Theory: {c.ExplanationFacts.Summary}");
                sb.AppendLine($"  Modal Colors: {string.Join(", ", c.ExplanationFacts.Tags.Where(t => t.StartsWith("Flavor:")))}");
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
        sb.AppendLine("Answer in a concise, helpful tone.");

        return sb.ToString();
    }
}
