namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Theory.Tonal;
using Microsoft.Extensions.AI;

/// <summary>
/// Suggests chord progressions for a given key and optional style.
/// </summary>
/// <remarks>
/// Hybrid approach: diatonic chord set is computed deterministically from the
/// <see cref="Key"/> domain model; the LLM selects idiomatic progressions and
/// explains them in a guitarist-friendly style.
/// </remarks>
public sealed class ProgressionSuggestionSkill(IChatClient chatClient, ILogger<ProgressionSuggestionSkill> logger)
    : AgentSkillBase(AgentIds.Theory, chatClient, logger), IOrchestratorSkill
{
    public override string Name        => "ProgressionSuggestion";
    public override string Description => "Suggests chord progressions for a given key and style";

    // ── Patterns ──────────────────────────────────────────────────────────────

    private static readonly Regex SuggestionTrigger = new(
        @"\b(suggest|give\s+me|show\s+me|common|typical|popular|example|create|make|what\s+are\s+some|i\s+need)\b.{0,50}\b(progressions?|chord\s+progressions?|chords)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeyPattern = new(
        @"\b([A-G][#b]?)\s*(major|minor|maj|min|m)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StylePattern = new(
        @"\b(blues|jazz|pop|rock|folk|country|classical|funk|soul|metal|flamenco|bossa\s*nova)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Diatonic quality suffixes: major = I M, II m, III m, IV M, V M, VI m, VII dim
    private static readonly string[] MajorQualities = ["", "m", "m", "", "", "m", "dim"];
    // Natural minor = I m, II dim, III M, IV m, V m, VI M, VII M
    private static readonly string[] MinorQualities = ["m", "dim", "", "m", "m", "", ""];

    // ── IOrchestratorSkill ────────────────────────────────────────────────────

    public override bool CanHandle(string message) =>
        SuggestionTrigger.IsMatch(message) &&
        (KeyPattern.IsMatch(message) || StylePattern.IsMatch(message));

    public override async Task<AgentResponse> ExecuteAsync(
        string message, CancellationToken cancellationToken = default)
    {
        // Extract key (if present)
        var keyMatch = KeyPattern.Match(message);
        var style = StylePattern.Match(message).Value;

        Key? key = null;
        if (keyMatch.Success)
        {
            var rootStr  = keyMatch.Groups[1].Value;
            var modeStr  = keyMatch.Groups[2].Value.ToLowerInvariant();
            var isMinor  = modeStr is "minor" or "min" or "m";
            key = Key.Items.FirstOrDefault(k =>
                k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
                string.Equals(k.Root.ToString(), rootStr, StringComparison.OrdinalIgnoreCase));
        }

        var prompt   = BuildPrompt(message, key, style);
        var response = await ChatAsync(message, prompt, cancellationToken);

        var keyName  = key is null ? "the given style" : $"{key.Root} {(key.KeyMode == KeyMode.Major ? "major" : "minor")}";
        var result   = ParseStructuredResponse(response, $"Chord progressions in {keyName}.");

        Logger.LogDebug("ProgressionSuggestionSkill: key={Key} style={Style}", keyName, style);
        return result;
    }

    // ── Prompt ────────────────────────────────────────────────────────────────

    private static string BuildPrompt(string message, Key? key, string style)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Theory Agent, a Guitar Alchemist music theory expert.");
        sb.AppendLine();
        sb.AppendLine($"The guitarist asked: \"{message}\"");
        sb.AppendLine();

        if (key is not null)
        {
            var notes     = key.Notes.ToList();
            var qualities = key.KeyMode == KeyMode.Major ? MajorQualities : MinorQualities;
            var diatonic  = notes.Select((n, i) => n + qualities[i]).ToArray();
            var keyName   = $"{key.Root} {(key.KeyMode == KeyMode.Major ? "major" : "minor")}";

            sb.AppendLine($"Key: **{keyName}**");
            sb.AppendLine($"Diatonic chords (use ONLY these unless you note a borrowed chord): {string.Join(", ", diatonic)}");
        }
        else
        {
            sb.AppendLine("No specific key was mentioned. Suggest progressions using standard diatonic chords.");
        }

        if (!string.IsNullOrWhiteSpace(style))
            sb.AppendLine($"Style: **{style}** — choose progressions idiomatic to this style.");

        sb.AppendLine("""

            Task: Suggest 3–4 chord progressions. For each:
            - Write the chords in order (e.g. "Am – F – C – G")
            - Give the Roman numerals (e.g. "i – ♭VI – ♭III – ♭VII")
            - Name the progression pattern (e.g. "vi–IV–I–V loop", "12-bar blues")
            - Write a one-sentence guitarist-friendly description

            Keep each progression 4–8 chords. Prefer progressions commonly used on guitar.

            Respond as valid JSON:
            {
              "result": "Your formatted progressions here (markdown)...",
              "confidence": 0.85,
              "evidence": ["key or style facts", "diatonic constraint applied"],
              "assumptions": ["any borrowed chords are noted"],
              "data": null
            }
            """);

        return sb.ToString();
    }
}
