namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using GA.Business.ML.Agents;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for chord-progression → key identification. Wraps
/// <see cref="KeyIdentificationService"/> so an LLM-driven SKILL.md skill
/// can call it deterministically and then phrase the answer in guitarist-
/// friendly language.
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Sixth tool in the MCP-
/// tool-exposure workstream — same template as the prior five
/// (<see cref="IntervalMcpTools"/> et al): length-guarded inputs, sanitized
/// Error echo via <see cref="McpEchoSanitizer"/>, structured result with
/// Error-branch invariant.
///
/// This is the FIRST hybrid-skill port: <see cref="Skills.KeyIdentificationSkill"/>
/// did key detection deterministically and then asked the LLM to phrase
/// the result. The C# path's prompt-building moves to the SKILL.md body;
/// the deterministic detection becomes this tool.
/// </remarks>
[McpServerToolType]
public sealed class KeyIdentificationMcpTools
{
    // The user message can be longer than a chord list — it might be
    // "What key is C Am F G in?" with surrounding prose. Cap at 256 to
    // accommodate that without inviting MB-sized abuse.
    private const int MaxQueryLength = 256;

    // Cap the candidate list — the LLM only needs the top tied + a couple
    // of partials to phrase a comparison ("could also be Eb major").
    private const int MaxPartialCandidates = 3;

    /// <summary>
    /// Identifies the most likely musical key(s) of a chord progression
    /// extracted from <paramref name="query"/>. Returns the candidates ranked
    /// by how many input chords are diatonic in each key.
    /// </summary>
    [McpServerTool(Name = "ga_key_identify"), Description(
        "Identify the musical key of a chord progression. " +
        "Pass either a bare chord list ('C Am F G') or the user's full question ('what key is C Am F G in?') — " +
        "the tool extracts the chord symbols and returns the ranked candidate keys with their diatonic sets. " +
        "Use whenever a user asks 'what key is X' / 'identify the key of X' / 'what key does X sound like'.")]
    public KeyIdentificationResult IdentifyKey(
        [Description("The user's question or a bare chord list. Examples: 'C Am F G', 'what key is Dm G C in?'.")]
        string query)
    {
        if (string.IsNullOrEmpty(query) || query.Length > MaxQueryLength)
            return KeyIdentificationResult.Failure(
                $"Could not parse '{McpEchoSanitizer.SanitizeEcho(query)}' — pass a chord list or question (e.g. 'C Am F G').");

        var chords = KeyIdentificationService.ExtractChords(query);
        if (chords.Count == 0)
            return KeyIdentificationResult.Failure(
                "No recognisable chord symbols found. Write them as standard chord names — e.g. 'Am F C G'.");

        var candidates = KeyIdentificationService.Identify(chords);
        if (candidates.Count == 0)
            return KeyIdentificationResult.Failure(
                $"No candidate key matches the progression [{string.Join(", ", chords)}].");

        var topScore = candidates[0].MatchCount;
        var topTied  = candidates
            .Where(c => c.MatchCount == topScore)
            .Select(ToCandidate)
            .ToArray();
        var partial  = candidates
            .Skip(topTied.Length)
            .Take(MaxPartialCandidates)
            .Select(ToCandidate)
            .ToArray();

        // Align top-level TotalChords with candidate-level TotalChords. The
        // KeyIdentificationService internally calls .Distinct() before computing
        // per-key match counts, so candidate.TotalChords reflects the de-duped
        // count. Using `chords.Count` here would silently disagree with each
        // candidate's `TotalChords` for inputs like "C Am F G C" (5 vs 4),
        // confusing the LLM payload. Bug surfaced by PR #88 review.
        return new KeyIdentificationResult
        {
            RecognizedChords = [.. chords],
            TopCandidates    = topTied,
            PartialMatches   = partial,
            TotalChords      = topTied[0].TotalChords,
        };
    }

    private static KeyCandidateInfo ToCandidate(KeyIdentificationService.KeyCandidate c) => new()
    {
        Key            = c.Key,
        RelativeKey    = c.RelativeKey,
        MatchCount     = c.MatchCount,
        TotalChords    = c.TotalChords,
        DiatonicSet    = c.DiatonicSet,
    };
}

/// <summary>One ranked candidate key.</summary>
public sealed record KeyCandidateInfo
{
    /// <summary>Canonical key name, e.g. <c>"C major"</c>, <c>"A minor"</c>.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Name of the relative major/minor key.</summary>
    public string RelativeKey { get; init; } = string.Empty;

    /// <summary>Number of input chords that are diatonic in this key.</summary>
    public int MatchCount { get; init; }

    /// <summary>Total number of chords in the input.</summary>
    public int TotalChords { get; init; }

    /// <summary>The seven diatonic chords of this key, in order I…vii°.</summary>
    public string[] DiatonicSet { get; init; } = [];
}

/// <summary>
/// Structured result of <see cref="KeyIdentificationMcpTools.IdentifyKey"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, all arrays are empty
/// and <see cref="TotalChords"/> is <c>0</c>. LLMs reading this record should
/// branch on <see cref="Error"/> first.
///
/// On success, <see cref="TopCandidates"/> is the set of keys tied at the
/// highest match count (often 1 element; sometimes 2 — e.g. C major and A
/// minor will tie because they share a diatonic set). <see cref="PartialMatches"/>
/// is up to <c>3</c> partial-match keys ranked behind the top set.
/// </remarks>
public sealed record KeyIdentificationResult
{
    /// <summary>The chord symbols the parser actually recognised in the query.</summary>
    public string[] RecognizedChords { get; init; } = [];

    /// <summary>Keys tied at the highest match count.</summary>
    public KeyCandidateInfo[] TopCandidates { get; init; } = [];

    /// <summary>Up to 3 partial-match keys ranked behind the top set.</summary>
    public KeyCandidateInfo[] PartialMatches { get; init; } = [];

    public int TotalChords { get; init; }

    /// <summary>Non-null when no chords could be parsed or no candidates matched.</summary>
    public string? Error { get; init; }

    public static KeyIdentificationResult Failure(string message) => new() { Error = message };
}
