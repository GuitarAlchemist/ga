namespace GA.Business.ML.Agents.Skills;

using System.Text;
using Microsoft.Extensions.AI;

/// <summary>
/// Identifies the musical key of a chord progression using pitch-class arithmetic
/// (<see cref="KeyIdentificationService"/>), then explains the result via LLM.
/// </summary>
/// <remarks>
/// Registered at the <b>orchestrator level</b> — fires before routing so no routing LLM
/// call is needed. The LLM is used only to explain the pre-computed domain result in
/// guitarist-friendly language; it never influences the key calculation itself.
/// </remarks>
public sealed class KeyIdentificationSkill(IChatClient chatClient, ILogger<KeyIdentificationSkill> logger)
    : AgentSkillBase(AgentIds.Theory, chatClient, logger), IOrchestratorSkill
{
    public override string Name        => "KeyIdentification";
    public override string Description => "Identifies the musical key of a chord progression using pitch-class arithmetic";

    public override bool CanHandle(string message) =>
        KeyIdentificationService.IsKeyIdentificationQuery(message);

    public override async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var chords     = KeyIdentificationService.ExtractChords(message);
        var candidates = KeyIdentificationService.Identify(chords);

        Logger.LogDebug("KeyIdentificationSkill: chords={Chords}, candidates={Count}",
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

        var topScore      = candidates[0].MatchCount;
        var topCandidates = candidates.Where(c => c.MatchCount == topScore).ToList();

        var prompt       = BuildPrompt(message, chords, topCandidates, candidates);
        var responseText = await ChatAsync(message, prompt, cancellationToken);
        var result       = ParseStructuredResponse(responseText, BuildFallback(chords, topCandidates));

        return result with
        {
            Evidence =
            [
                .. result.Evidence,
                $"Chords analysed: {string.Join(", ", chords)}",
                .. topCandidates.Select(c =>
                    $"{c.Key} — {c.MatchCount}/{c.TotalChords} chords diatonic " +
                    $"(set: {string.Join(", ", c.DiatonicSet)})")
            ]
        };
    }

    // ── Prompt helpers ────────────────────────────────────────────────────────

    private static string BuildPrompt(
        string message,
        IReadOnlyList<string> chords,
        IReadOnlyList<KeyIdentificationService.KeyCandidate> top,
        IReadOnlyList<KeyIdentificationService.KeyCandidate> all)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""
            You are Theory Agent, a Guitar Alchemist music theory expert.

            The guitarist asked: "{message}"

            I have already computed which keys fit the chord progression [{string.Join(", ", chords)}].
            Use ONLY the data below — do not guess or add your own analysis.
            """);

        sb.AppendLine("── TOP MATCHES (all tied at the highest score) ──");
        foreach (var c in top)
        {
            sb.AppendLine($"• {c.Key}  ({c.MatchCount}/{c.TotalChords} chords diatonic)");
            sb.AppendLine($"  Relative key : {c.RelativeKey}");
            sb.AppendLine($"  Diatonic set : {string.Join(", ", c.DiatonicSet)}");
        }

        if (all.Count > top.Count)
        {
            sb.AppendLine();
            sb.AppendLine("── PARTIAL MATCHES ──");
            foreach (var c in all.Skip(top.Count).Take(3))
                sb.AppendLine($"• {c.Key}  ({c.MatchCount}/{c.TotalChords} chords diatonic)");
        }

        sb.AppendLine("""

            Instructions:
            1. Explain which key (or keys) the progression is most likely in and WHY.
            2. If two keys tie (e.g. C major and A minor), explain how to distinguish them
               by listening for the tonic chord or melodic resolution.
            3. Give the Roman numeral function of each input chord in the most likely key.
            4. Mention the natural scale (e.g. "C major scale: C D E F G A B").
            5. Keep the answer guitarist-friendly — no jargon without explanation.

            IMPORTANT: respond as valid JSON:
            {
              "result": "Your full explanation here...",
              "confidence": 0.0–1.0,
              "evidence": ["fact 1", "fact 2"],
              "assumptions": ["assumption 1"],
              "data": null
            }
            """);

        return sb.ToString();
    }

    private static string BuildFallback(
        IReadOnlyList<string> chords,
        IReadOnlyList<KeyIdentificationService.KeyCandidate> top)
    {
        if (top.Count == 0)
            return $"Could not determine a key for: {string.Join(", ", chords)}";

        var keyList = string.Join(" / ", top.Select(c => c.Key));
        return $"The progression [{string.Join(", ", chords)}] fits: {keyList}. " +
               $"Diatonic set: {string.Join(", ", top[0].DiatonicSet)}.";
    }
}
