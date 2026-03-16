namespace GA.Business.ML.Agents.Skills;

using System.Text;
using GA.Domain.Core.Theory.Tonal;
using Microsoft.Extensions.AI;

/// <summary>
/// Provides Roman numeral harmonic analysis of a chord progression.
/// </summary>
/// <remarks>
/// Hybrid approach: key detection and diatonic scoring use
/// <see cref="KeyIdentificationService"/> (pure pitch-class arithmetic); the LLM explains
/// the harmonic function of each chord in guitarist-friendly language.
/// </remarks>
public sealed class HarmonicAnalysisSkill(IChatClient chatClient, ILogger<HarmonicAnalysisSkill> logger)
    : AgentSkillBase(AgentIds.Theory, chatClient, logger), IOrchestratorSkill
{
    public override string Name        => "HarmonicAnalysis";
    public override string Description => "Provides Roman numeral analysis and harmonic function for a chord progression";

    // ── Pattern ───────────────────────────────────────────────────────────────

    // Matches analysis / function queries that aren't already handled by KeyIdentificationSkill
    private static readonly System.Text.RegularExpressions.Regex AnalysisTrigger = new(
        @"\b(analyze|analyse|analysis|harmonic|function\s+of|roman\s+numeral|what\s+(?:function|role)|describe\s+(?:the\s+chords?|this\s+progression)|break\s+down)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // ── IOrchestratorSkill ────────────────────────────────────────────────────

    public override bool CanHandle(string message) =>
        AnalysisTrigger.IsMatch(message) &&
        KeyIdentificationService.ExtractChords(message).Count >= 2;

    public override async Task<AgentResponse> ExecuteAsync(
        string message, CancellationToken cancellationToken = default)
    {
        var chords     = KeyIdentificationService.ExtractChords(message);
        var candidates = KeyIdentificationService.Identify(chords);

        Logger.LogDebug("HarmonicAnalysisSkill: chords={Chords}, candidates={Count}",
            string.Join(" ", chords), candidates.Count);

        if (candidates.Count == 0)
            return new AgentResponse
            {
                AgentId     = AgentId,
                Result      = "I couldn't find recognisable chord symbols in your question. " +
                              "Please write them as standard chord names, e.g. \"Am F C G\".",
                Confidence  = 0.3f,
                Evidence    = [],
                Assumptions = ["No parseable chord symbols found"]
            };

        var top    = candidates[0];
        var topTied = candidates.Where(c => c.MatchCount == top.MatchCount).ToList();
        var prompt  = BuildPrompt(message, chords, top, topTied);
        var responseText = await ChatAsync(message, prompt, cancellationToken);

        return ParseStructuredResponse(responseText, BuildFallback(chords, top)) with
        {
            Evidence =
            [
                $"Chords analysed: {string.Join(", ", chords)}",
                $"Detected key: {top.Key} ({top.MatchCount}/{top.TotalChords} chords diatonic)",
                $"Diatonic set: {string.Join(", ", top.DiatonicSet)}",
            ]
        };
    }

    // ── Prompt helpers ────────────────────────────────────────────────────────

    private static string BuildPrompt(
        string message,
        IReadOnlyList<string> chords,
        KeyIdentificationService.KeyCandidate top,
        IReadOnlyList<KeyIdentificationService.KeyCandidate> topTied)
    {
        var keyDesc = topTied.Count == 1
            ? top.Key
            : string.Join(" / ", topTied.Select(c => c.Key));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            You are Theory Agent, a Guitar Alchemist music theory expert.

            The guitarist asked: "{{message}}"

            Chord progression to analyse: [{{string.Join(", ", chords)}}]
            Most likely key: {{keyDesc}}  ({{top.MatchCount}}/{{top.TotalChords}} chords diatonic)
            Diatonic set: {{string.Join(", ", top.DiatonicSet)}}
            """);

        if (topTied.Count > 1)
        {
            sb.AppendLine("Note: two keys tie (relative major/minor). Analyse in the most likely interpretation.");
        }

        sb.AppendLine("""

            Task — provide a complete harmonic analysis:
            1. State the key and why it is the most likely choice.
            2. For each chord give:
               - Roman numeral (e.g. IV, ii, V7, ♭VII)
               - Harmonic function: tonic (T), subdominant (S), or dominant (D)
               - Brief note if it's a borrowed/non-diatonic chord
            3. Describe the overall harmonic motion (e.g. "authentic cadence", "deceptive cadence", "pedal point").
            4. Note any voice-leading highlights (common tones, half-step motion).

            Keep the answer guitarist-friendly — avoid jargon without explanation.

            Respond as valid JSON:
            {
              "result": "Your full harmonic analysis here (markdown)...",
              "confidence": 0.0–1.0,
              "evidence": ["key detection fact", "notable harmonic movement"],
              "assumptions": ["any ambiguous interpretations noted"],
              "data": null
            }
            """);

        return sb.ToString();
    }

    private static string BuildFallback(
        IReadOnlyList<string> chords,
        KeyIdentificationService.KeyCandidate top) =>
        $"Progression [{string.Join(", ", chords)}] is most likely in {top.Key}. " +
        $"Diatonic set: {string.Join(", ", top.DiatonicSet)}.";
}
