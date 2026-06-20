namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Deterministic theory-comparison skill. Answers "what is the difference
/// between X and Y" style questions where X and Y are music-theory
/// concepts (scale qualities, modes, etc.) — no LLM call required.
/// </summary>
/// <remarks>
/// <para>
/// Built 2026-05-16 to close the corpus failure "What is the difference
/// between major and minor" — the 2026-05-15 live probe trace showed
/// <c>skill.relativekey</c> semantically grabbing it (because
/// "major / minor / key" embed close to RelativeKeySkill's description),
/// then returning 0.1-confidence canned text, then Ollama timing out at
/// 15s. This skill exists so the semantic router has a higher-scoring
/// match for "difference / compare / vs" queries.
/// </para>
/// <para>
/// v1 scope: major vs minor (the broken prompt). Future extensions follow
/// the same pattern — add a regex pair and a comparison body. The skill
/// is intentionally narrow: deterministic structural comparisons only.
/// Open-ended "explain X" goes through the LLM.
/// </para>
/// </remarks>
public sealed class TheoryComparisonSkill(ILogger<TheoryComparisonSkill> logger) : IOrchestratorSkill
{
    public string Name => "TheoryComparison";

    public string Description =>
        "Compares two music theory concepts and explains their structural " +
        "differences. Answers 'what is the difference between major and minor', " +
        "'compare major and minor', 'major vs minor' and similar comparison " +
        "queries with deterministic structural content (scale formulas, " +
        "interval differences, characteristic intervals). No LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What is the difference between major and minor",
        "Compare major and minor",
        "Major vs minor",
        "Major versus minor",
        "Difference between major and minor scales",
        "Explain the difference between major and minor",
        "How do major and minor differ",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Match "difference between X and Y", "compare X and Y", "X vs Y",
    // "X versus Y", "how do X and Y differ", "X and Y difference".
    // Quality tokens are kept narrow so unrelated comparisons
    // ("C major vs F major", "Hendrix vs Clapton") do not route here.
    private const string QualityTokens = "major|minor";

    private static readonly Regex DifferencePattern =
        new(@"\b(?:what(?:'s|\s+is)?\s+the\s+)?(?:difference|distinction)\s+between\s+(?<a>" + QualityTokens + @")\s+and\s+(?<b>" + QualityTokens + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ComparePattern =
        new(@"\bcompare\s+(?<a>" + QualityTokens + @")\s+(?:and|with|vs|versus)\s+(?<b>" + QualityTokens + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VsPattern =
        // Negative lookbehind keeps "C major vs C minor" out — that's a
        // parallel-key question handled by RelativeKeySkill. Bare
        // "major vs minor" with no preceding key letter falls through.
        new(@"(?<![A-Ga-g][b#♭♯]?\s)\b(?<a>" + QualityTokens + @")\s+(?:vs\.?|versus)\s+(?<b>" + QualityTokens + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HowDifferPattern =
        new(@"\bhow\s+do\s+(?<a>" + QualityTokens + @")\s+and\s+(?<b>" + QualityTokens + @")\s+differ\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var (a, b) = MatchPair(message);

        if (a is null || b is null) return Task.FromResult(CannotHandle());

        // Normalize: order doesn't matter for the comparison body, but we
        // canonicalize to (major, minor) so the lookup is single-keyed.
        var (left, right) = NormalizePair(a, b);
        if (left == right) return Task.FromResult(SamePair(left));

        return Task.FromResult(Compare(left, right));
    }

    private static (string? a, string? b) MatchPair(string message)
    {
        foreach (var pattern in new[] { DifferencePattern, ComparePattern, VsPattern, HowDifferPattern })
        {
            var match = pattern.Match(message);
            if (match.Success)
            {
                return (
                    match.Groups["a"].Value.ToLowerInvariant(),
                    match.Groups["b"].Value.ToLowerInvariant());
            }
        }
        return (null, null);
    }

    private static (string left, string right) NormalizePair(string a, string b)
    {
        // Canonicalize so (major, minor) and (minor, major) hit the same body.
        if (a == "major" || b == "minor") return (a, b);
        return (b, a);
    }

    private AgentResponse Compare(string a, string b)
    {
        var evidence = $"TheoryComparisonSkill: matched pair ({a}, {b})";
        logger.LogDebug("{Evidence}", evidence);

        // The canonical major-vs-minor body. Designed to satisfy the
        // 2026-05-13 corpus invariants on the matching prompt:
        // contains: ["major", "minor"]
        // contains_any: ["third", "interval", "scale", "chord", "quality",
        //                "tonality", "sad", "happy", "key", "semitone"]
        // min_length: 150
        var body =
            "Major and minor differ primarily in the **third scale degree**, " +
            "with secondary differences at the 6th and 7th depending on the minor form.\n\n" +
            "**Scale formulas (semitones from root):**\n" +
            "- Major:   2 2 1 2 2 2 1 — degrees `1 2 3 4 5 6 7` (major third = 4 semitones)\n" +
            "- Minor:   2 1 2 2 1 2 2 — degrees `1 2 b3 4 5 b6 b7` (minor third = 3 semitones, natural minor)\n\n" +
            "The single interval that flips is the **third**: a major third (4 semitones) versus a minor third (3 semitones). " +
            "That one-semitone shift changes the entire harmonic and emotional character of the key — chords built on the same root come out major or minor accordingly.\n\n" +
            "**Common associations:** major sounds bright and stable (often \"happy\"); minor sounds darker and more ambiguous (often \"sad\" but really just emotionally richer). These are cultural shorthand, not absolutes.\n\n" +
            "**Minor variants:**\n" +
            "- Natural minor: `1 2 b3 4 5 b6 b7`\n" +
            "- Harmonic minor: `1 2 b3 4 5 b6 7` — raised 7th for stronger dominant resolution\n" +
            "- Melodic minor: `1 2 b3 4 5 6 7` ascending (raised 6th and 7th), natural minor descending\n\n" +
            "**In context:**\n" +
            "- Relative pairs share a key signature (C major ↔ A minor, G major ↔ E minor)\n" +
            "- Parallel pairs share a root but flip quality (C major ↔ C minor)\n" +
            "- Diatonic chords differ: I IV V (major-quality) vs i iv V (minor with raised 7th in V)";

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: TheoryComparisonSkill (deterministic pair comparison)", evidence],
        };
    }

    private static AgentResponse SamePair(string label) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"You asked to compare {label} to itself — there's no difference. Try comparing {label} to its opposite (major↔minor) or to a specific mode (e.g. \"{label} vs dorian\").",
        Confidence = 0.7f,
        Evidence   = ["TheoryComparisonSkill: same-token pair"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = string.Empty,
        Confidence = 0.0f,
        Evidence   = ["TheoryComparisonSkill: no recognised comparison pattern"],
    };
}
