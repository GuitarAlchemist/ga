namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Computes fret span and playability for a chord diagram — zero LLM calls, pure arithmetic.
/// </summary>
/// <remarks>
/// Registered at the <b>orchestrator level</b>. Handles queries that include a
/// dash-separated or compact chord diagram (e.g. <c>x-3-2-0-1-0</c> or <c>x32010</c>)
/// together with a question about stretch, reach, or playability.
/// </remarks>
public sealed class FretSpanSkill(ILogger<FretSpanSkill> logger) : IOrchestratorSkill
{
    public string Name        => "FretSpan";
    public string Description => "Computes fret span and playability rating from a chord diagram";

    // Dash-separated: x-3-2-0-1-0  or  0-2-2-1-0-0
    private static readonly Regex DashDiagram =
        new(@"\b([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})\b",
            RegexOptions.Compiled);

    // Compact starting with x: x32010, x02120, etc.
    private static readonly Regex CompactDiagram =
        new(@"\b[xX]\d{5}\b", RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        var hasDiagram = DashDiagram.IsMatch(message) || CompactDiagram.IsMatch(message);
        if (!hasDiagram) return false;

        var q = message.ToLowerInvariant();
        return q.Contains("span")      || q.Contains("stretch") || q.Contains("reach") ||
               q.Contains("playab")    || q.Contains("hard")    || q.Contains("difficult") ||
               q.Contains("fingering") || q.Contains("fret");
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var frets = TryParseFrets(message);
        if (frets is null)
            return Task.FromResult(CannotHelp("Could not parse a chord diagram from your message."));

        var pressed = frets.Where(f => f > 0).ToList();
        if (pressed.Count == 0)
            return Task.FromResult(CannotHelp("All strings are open or muted — no fret span to compute."));

        var minFret = pressed.Min();
        var maxFret = pressed.Max();
        var span = maxFret - minFret;

        var difficultyDesc = span switch
        {
            0 or 1 => "very easy — all fretted notes sit in adjacent positions",
            2      => "easy — comfortable stretch for most players",
            3      => "moderate — a normal left-hand extension",
            4      => "challenging — requires a significant stretch; warm up first",
            _      => $"very wide — span of {span} frets may be difficult for smaller hands"
        };

        // Playability score: 1 (easy) → 10 (very hard), capped
        var playabilityScore = int.Clamp(1 + span * 2, 1, 10);

        var diagram = string.Join("-", frets.Select(f => f < 0 ? "x" : f.ToString()));
        logger.LogDebug("FretSpanSkill: {Diagram} → span {Span}", diagram, span);

        var result = new StringBuilder();
        result.Append($"Chord diagram **{diagram}**: ");
        result.Append($"fret span is **{span}** (frets {minFret}–{maxFret}). ");
        result.AppendLine($"Difficulty: {difficultyDesc}.");
        if (span >= 4)
            result.AppendLine("Consider whether a capo or alternative voicing might be more comfortable.");

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Technique,
            Result     = result.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Diagram: {diagram}",
                $"Fretted positions: {string.Join(", ", pressed)}",
                $"Span: fret {minFret} to fret {maxFret} = {span} frets",
                $"Playability score: {playabilityScore}/10"
            ],
            Assumptions = ["Standard 6-string guitar; strings listed low-E to high-e"]
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns 6 fret values (-1 = muted) or null if no diagram found.</summary>
    private static List<int>? TryParseFrets(string message)
    {
        // Try dash-separated first
        var dash = DashDiagram.Match(message);
        if (dash.Success)
        {
            return Enumerable.Range(1, 6)
                .Select(i => ParseFret(dash.Groups[i].Value))
                .ToList();
        }

        // Try compact (e.g. x32010)
        var compact = CompactDiagram.Match(message);
        if (!compact.Success) return null;

        var val = compact.Value;
        return val.Select(c => c is 'x' or 'X' ? -1 : c - '0').ToList();
    }

    private static int ParseFret(string s) =>
        s.Equals("x", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(s);

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Technique,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };
}
