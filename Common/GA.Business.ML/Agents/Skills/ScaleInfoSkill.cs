namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Answers "what notes are in X major/minor?" queries using <see cref="Key.Items"/> —
/// zero LLM calls, pure domain computation.
/// </summary>
/// <remarks>
/// Registered at the <b>orchestrator level</b>. Returns the 7 scale notes and the
/// relative key as structured evidence without touching the LLM pipeline.
/// </remarks>
public sealed class ScaleInfoSkill(ILogger<ScaleInfoSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ScaleInfo";
    public string Description =>
        "Returns the notes of a major or minor key (e.g. C major has C D E F G A B). " +
        "Pure domain computation, zero LLM calls.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What notes are in C major?",
        "Show me the F# minor scale",
        "List the notes in Bb major",
        // Bare "What is X major/minor?" pattern — common phrasing that
        // production was dropping into the LLM fallback because no example
        // prompt was structurally close enough to it. The 0.65 cosine
        // threshold + "What is a C major chord?" gap on the ChordInfo side
        // pushed both candidates below threshold for the bare query.
        // See docs/plans/2026-05-03-skill-routing-quality-fix.md for the
        // diagnosis trail.
        "What is C major?",
        "What is A minor?",
        "What is D minor?",
        "What is F# major?",
        "Tell me the notes of E major",
        // "What's in the [X] major/minor scale" pattern — was losing to
        // ModesSkill because "scale" appeared adjacent to a key letter
        // and the modes embedding was a slightly better cosine match.
        // Added 2026-05-12 to close si-4 misroute in the 2026-05-11 corpus.
        "What's in the G major scale?",
        "What's in the D minor scale?",
        "What's in the F major scale?",
        // v0.5 corpus expansion (2026-05-12): "formula for [scale]"
        // pattern — was misrouting to chordinfo on the abstract
        // "formula" word. Scale formulas are scale knowledge.
        "What's the formula for harmonic minor",
        "Formula for melodic minor scale",
        "Degrees of the A major scale",
        // "Show me the notes in [key]" family — bare key, no "scale"/"chord"
        // word. "Show me the notes in C major" was losing to ChordInfoSkill
        // because "notes in" + "C major" sat closer to the chord examples
        // ("What notes are in a Cmaj7?") than to any scaleinfo anchor, which
        // all carried either "scale" or the "What notes are in…?" framing.
        // Added 2026-06-19 to close the scales-keys misroute surfaced by the
        // /auto-optimize loop (skill.scaleinfo expected, skill.chordinfo seen).
        "Show me the notes in C major",
        "Show me the notes in A minor",
        "Show me the notes in G major",
    ];

    // Matches: "notes in C major", "what is Bb minor scale", "D# minor notes", etc.
    private static readonly Regex KeyPattern =
        new(@"\b([A-G][#b]?)\s*(major|minor|maj|min)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var q = message.ToLowerInvariant();

        // Yield to ChordInfoSkill when the prompt is clearly about a chord rather
        // than a scale — "what notes are in a C major chord?" otherwise matches
        // both because "C major" + "note" satisfies our pattern.
        if (q.Contains("chord")) return false;

        return KeyPattern.IsMatch(message) &&
               (q.Contains("note") || q.Contains("scale") || q.Contains("what is") ||
                q.Contains("what's in") || q.Contains("tell me") || q.Contains("show me") ||
                q.Contains("list") || q.Contains("play"));
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var match = KeyPattern.Match(message);
        if (!match.Success)
            return Task.FromResult(CannotHelp("Could not parse a key name from your question."));

        var rootStr  = match.Groups[1].Value;
        var modeStr  = match.Groups[2].Value.ToLowerInvariant();
        var isMinor  = modeStr is "minor" or "min";

        // Find the matching domain key
        var key = KeyNaming.ResolveKey(rootStr, isMinor);

        if (key is null)
            return Task.FromResult(CannotHelp(
                $"I don't recognise \"{rootStr} {modeStr}\" as a standard key. " +
                "Try a key like C major, F# minor, or Bb major."));

        var notes       = key.Notes.ToList();
        var noteNames   = notes.Select(n => n.ToString()).ToList();
        var keyName     = $"{key.Root} {(isMinor ? "minor" : "major")}";
        var relativeKey = key.KeyMode == KeyMode.Major
            ? $"Relative minor: {KeyNaming.RelativeKeyName(key)}"
            : $"Relative major: {KeyNaming.RelativeKeyName(key)}";

        logger.LogDebug("ScaleInfoSkill: resolved {Key} → [{Notes}]", keyName, string.Join(", ", noteNames));

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = $"The {keyName} scale has 7 notes: **{string.Join(" – ", noteNames)}**. {relativeKey}.",
            Confidence = 1.0f,
            Evidence   =
            [
                $"Key: {keyName}",
                $"Notes: {string.Join(", ", noteNames)}",
                $"Key signature: {KeyNaming.DescribeKeySignature(key)}",
                relativeKey
            ],
            Assumptions = []
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };
}
