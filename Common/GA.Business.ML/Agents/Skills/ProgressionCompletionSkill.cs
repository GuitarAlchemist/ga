namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

/// <summary>
/// Suggests 2–3 diatonic chord completions for an in-progress progression.
/// </summary>
/// <remarks>
/// Hybrid approach: <see cref="KeyIdentificationService"/> detects the key and diatonic set
/// deterministically; the LLM selects and explains cadence candidates from that pre-computed set.
/// Emits an AG-UI <c>ga:completion-suggestions</c> event in <see cref="AgentResponse.Data"/>.
/// </remarks>
public sealed class ProgressionCompletionSkill(IChatClient chatClient, ILogger<ProgressionCompletionSkill> logger)
    : AgentSkillBase(AgentIds.Theory, chatClient, logger), IOrchestratorSkill
{
    public override string Name        => "ProgressionCompletion";
    public override string Description => "Suggests diatonic chord completions for an in-progress progression";

    // ── Triggers ──────────────────────────────────────────────────────────────

    private static readonly Regex CompletionTrigger = new(
        @"\b(finish|complete|end\s+it|end\s+this|what\s+comes\s+next|next\s+chord|help\s+me\s+finish|how\s+(do\s+i|to)\s+end|what\s+should\s+follow|continue|extend)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChordSymbolPattern = new(
        @"\b[A-G][b#]?(?:m|maj|dim|aug|7|maj7|m7|m7b5|dim7)?\b",
        RegexOptions.Compiled);

    public override bool CanHandle(string message) =>
        CompletionTrigger.IsMatch(message) &&
        ChordSymbolPattern.Matches(message).Count >= 2;

    public override async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var chords     = KeyIdentificationService.ExtractChords(message);
        var candidates = KeyIdentificationService.Identify(chords);

        Logger.LogDebug("ProgressionCompletionSkill: chords={Chords}, candidates={Count}",
            string.Join(" ", chords), candidates.Count);

        if (candidates.Count == 0)
            return new AgentResponse
            {
                AgentId     = AgentId,
                Result      = "I couldn't find recognisable chord symbols in your question. " +
                              "Please write them as standard chord names, e.g. \"Am F C\".",
                Confidence  = 0.3f,
                Evidence    = [],
                Assumptions = ["No parseable chord symbols found"]
            };

        var top          = candidates[0];
        var prompt       = BuildPrompt(message, chords, top, candidates);
        var responseText = await ChatAsync(message, prompt, cancellationToken);
        var result       = ParseStructuredResponse(responseText, BuildFallback(chords, top));

        return result with
        {
            Evidence =
            [
                .. result.Evidence,
                $"Chords analysed: {string.Join(", ", chords)}",
                $"Detected key: {top.Key}",
                $"Diatonic set: {string.Join(", ", top.DiatonicSet)}"
            ]
        };
    }

    // ── Prompt helpers ────────────────────────────────────────────────────────

    private static string BuildPrompt(
        string message,
        IReadOnlyList<string> chords,
        KeyIdentificationService.KeyCandidate top,
        IReadOnlyList<KeyIdentificationService.KeyCandidate> all)
    {
        var sb = new StringBuilder();

        var topTied = all.Where(c => c.MatchCount == top.MatchCount).ToList();
        var keyDesc = topTied.Count == 1
            ? top.Key
            : string.Join(" / ", topTied.Select(c => c.Key));

        sb.AppendLine($$"""
            You are Theory Agent, a Guitar Alchemist music theory expert.

            The guitarist asked: "{{message}}"

            The input progression is: [{{string.Join(", ", chords)}}]
            Detected key: {{keyDesc}}  ({{top.MatchCount}}/{{top.TotalChords}} chords diatonic)

            AVAILABLE DIATONIC CHORDS — you may ONLY suggest chords from this list:
            {{string.Join(", ", top.DiatonicSet)}}

            Task: Suggest 2-3 chord completions (each 1-2 chords) that cadence naturally
            to end or continue the progression in {{keyDesc}}.

            For each suggestion:
              - Name the cadence type (authentic, half, deceptive, or plagal)
              - Give the Roman numeral(s)
              - Write a one-sentence guitarist-friendly explanation

            IMPORTANT: Every chord you suggest MUST appear in the AVAILABLE DIATONIC CHORDS list.
            You may substitute the plain V chord with V7 even if only V appears in the diatonic list
            (this is the standard harmonic minor adjustment).

            Respond as valid JSON:
            {
              "result": "Your formatted completion suggestions here (markdown)...",
              "confidence": 0.9,
              "evidence": ["key detection fact", "diatonic constraint applied"],
              "assumptions": ["suggestions are strictly diatonic"],
              "data": {
                "event": "ga:completion-suggestions",
                "suggestions": [
                  { "chords": ["E7"], "cadence": "authentic", "roman": "V7-i", "explanation": "Strongest resolution back to Am." },
                  { "chords": ["G"],  "cadence": "half",      "roman": "bVII-i", "explanation": "Open loop, floats back to the top." }
                ]
              }
            }
            """);

        return sb.ToString();
    }

    private static string BuildFallback(
        IReadOnlyList<string> chords,
        KeyIdentificationService.KeyCandidate top) =>
        $"Progression [{string.Join(", ", chords)}] is in {top.Key}. " +
        $"Diatonic completions from: {string.Join(", ", top.DiatonicSet)}.";
}
