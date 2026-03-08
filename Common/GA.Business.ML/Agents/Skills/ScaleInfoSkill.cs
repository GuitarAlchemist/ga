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
    public string Description => "Returns the notes of a major or minor key from the domain model";

    // Matches: "notes in C major", "what is Bb minor scale", "D# minor notes", etc.
    private static readonly Regex KeyPattern =
        new(@"\b([A-G][#b]?)\s*(major|minor|maj|min)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        var q = message.ToLowerInvariant();
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
        var key = Key.Items.FirstOrDefault(k =>
            k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
            string.Equals(k.Root.ToString(), rootStr, StringComparison.OrdinalIgnoreCase));

        if (key is null)
            return Task.FromResult(CannotHelp(
                $"I don't recognise \"{rootStr} {modeStr}\" as a standard key. " +
                "Try a key like C major, F# minor, or Bb major."));

        var notes       = key.Notes.ToList();
        var noteNames   = notes.Select(n => n.ToString()).ToList();
        var keyName     = $"{key.Root} {(isMinor ? "minor" : "major")}";
        var relativeKey = key.KeyMode == KeyMode.Major
            ? $"Relative minor: {GetRelativeName(key)}"
            : $"Relative major: {GetRelativeName(key)}";

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
                $"Key signature: {DescribeKeySignature(key)}",
                relativeKey
            ],
            Assumptions = []
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetRelativeName(Key key)
    {
        // Relative pair shares the same pitch-class set — find by PC mask
        var mask    = key.Notes.Aggregate(0, (acc, n) => acc | (1 << n.PitchClass.Value));
        var sibling = Key.Items.FirstOrDefault(k =>
            k.KeyMode != key.KeyMode &&
            k.Notes.Aggregate(0, (acc, n) => acc | (1 << n.PitchClass.Value)) == mask);

        return sibling is null ? "none" : $"{sibling.Root} {(sibling.KeyMode == KeyMode.Major ? "major" : "minor")}";
    }

    private static string DescribeKeySignature(Key key)
    {
        var count = key.KeySignature.AccidentalCount;
        if (count == 0) return "no sharps or flats";
        var kind  = key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "sharp" : "flat";
        return $"{count} {kind}{(count > 1 ? "s" : "")}";
    }

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };
}
