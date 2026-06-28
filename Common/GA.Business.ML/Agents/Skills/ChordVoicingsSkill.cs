namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.ML.Search;

/// <summary>
/// Returns playable guitar chord voicings for a named chord, mode, or
/// technique tag. Thin skill-layer wrapper over the same domain pipeline
/// VoicingAgent uses (typed extractor → MusicalQueryEncoder → search).
/// Pure domain compute when the parser finds a chord/mode/tag; returns a
/// "no intent" response otherwise (no LLM fallback at the skill layer).
/// </summary>
// @ai:business-value top-1 chatbot skill by traffic — voicing lookup is the canonical chord-question flow we sell the product on [T:manually-reviewed conf:0.92 src:product-owner@2026-05-24]
[GuitarAlchemist.Registry.GaSkill("ChordVoicings", "voicing")]
public sealed partial class ChordVoicingsSkill(
    ILogger<ChordVoicingsSkill> logger,
    EnhancedVoicingSearchService voicingSearch,
    IMusicalQueryExtractor extractor,
    MusicalQueryEncoder encoder) : IOrchestratorSkill
{
    public string Name => "ChordVoicings";

    public string Description =>
        "Finds playable guitar chord voicings for a named chord, mode, or " +
        "technique tag (drop2, shell, rootless, quartal, barre). Returns " +
        "specific fingerings with diagrams (e.g. x-3-2-0-1-0) ranked by " +
        "musical similarity over OPTIC-K geometry. Pure domain retrieval, " +
        "no LLM calls.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "voicings for Cmaj7",
        "show me Dm7 voicings",
        "shapes for F major",
        "fingerings for G7",
        "Cmaj9 voicings",
        "drop2 voicings of Cmaj7",
        "shell voicing for Dm7",
        "rootless A7 voicings",
        "quartal voicings in C",
        "all C major voicings on guitar",
        "open chord shape for E minor",
        "barre voicings for Bb major",
    ];

    // Intent keywords. Require an explicit voicing/shape/fingering verb +
    // a chord-shaped token so we don't grab generic chord-questions which
    // belong to ChordInfoSkill (those just want notes inside a chord).
    private static readonly string[] VoicingKeywords =
    [
        "voicing", "voicings", "shape", "shapes", "fingering", "fingerings",
        "drop2", "drop-2", "drop3", "drop-3", "shell", "rootless", "quartal",
        "open chord", "open shape", "barre voicing", "barré voicing",
    ];

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var q = message.ToLowerInvariant();
        // Whole-word match so a keyword embedded in an unrelated word doesn't
        // fire (e.g. "shell" inside "PowerShell" — see ga#261).
        var hasVoicingIntent = VoicingKeywords.Any(k => ChordIntentMatching.ContainsWord(q, k));
        if (!hasVoicingIntent) return false;
        // Require a real chord token. The two regexes are case-sensitive on the
        // root so bare lowercase "a"/"e" in normal English ("show me a shape")
        // won't trigger. Strict form requires an accidental/quality/digit
        // immediately after the root (Cmaj7, G7, F#m); spaced form allows
        // "[A-G] major/minor/dim/aug/sus" with whitespace between root and
        // quality (Bb major, C minor).
        return ChordSuffixRegex().IsMatch(message)
               || ChordWithSpacedQualityRegex().IsMatch(message);
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var structured = await extractor.ExtractAsync(message, cancellationToken).ConfigureAwait(false);
        var hasIntent = structured.ChordSymbol is not null
                        || structured.PitchClasses is { Length: > 0 }
                        || structured.ModeName is not null
                        || structured.Tags is { Count: > 0 };

        if (!hasIntent)
        {
            return new AgentResponse
            {
                AgentId = $"skill.{Name.ToLowerInvariant()}",
                Result =
                    "I couldn't find a chord name, mode, or technique tag in your request. " +
                    "Try naming a chord (e.g. 'Cmaj7'), a mode ('F# Lydian'), or a " +
                    "technique ('drop2', 'shell', 'rootless').",
                Confidence = 0.2f,
                Evidence = [],
                Assumptions = ["Query contained no musical entities recognizable by the typed parser."],
            };
        }

        var queryVector = encoder.Encode(structured);
        var filters = VoicingAgent.BuildSearchFilters(structured);

        Task<double[]> Generator(string _) => Task.FromResult(queryVector);
        var results = await voicingSearch.SearchAsync(message, Generator, topK: 8, filters, cancellationToken).ConfigureAwait(false);

        // Corpus-tagging mismatch workaround (docs/solutions/architecture/2026-05-08-
        // voicing-search-corpus-tagging-mismatch.md): filters target Tags/ChordName
        // fields the corpus doesn't populate, so filter-bearing queries reliably
        // return 0 hits. Retry without filters before reporting failure.
        if (results.Count == 0 && filters is not null)
        {
            logger.LogDebug(
                "ChordVoicingsSkill: zero hits with filters for {Intent}, retrying without filters",
                structured.ChordSymbol ?? structured.ModeName ?? "(tags)");
            results = await voicingSearch.SearchAsync(message, Generator, topK: 8, null, cancellationToken).ConfigureAwait(false);
        }

        if (results.Count == 0)
        {
            return new AgentResponse
            {
                AgentId = $"skill.{Name.ToLowerInvariant()}",
                Result = "The OPTIC-K index returned no matches for this query.",
                Confidence = 0.3f,
                Evidence = [],
                Assumptions = ["Corpus does not contain voicings matching this musical profile."],
                Data = new { interpreted = structured },
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} voicing{(results.Count == 1 ? "" : "s")}:");
        sb.AppendLine();
        foreach (var r in results)
        {
            sb.AppendLine(
                $"- **{r.Document.ChordName ?? "Voicing"}** `{r.Document.Diagram}` " +
                $"({r.Document.VoicingType ?? "guitar"}, score {r.Score:F3})");
        }

        var evidence = results
            .Take(Math.Min(results.Count, 5))
            .Select(r => $"{r.Document.ChordName ?? "?"} · {r.Document.Diagram} · score={r.Score:F4}")
            .ToList();

        logger.LogDebug(
            "ChordVoicingsSkill: returned {Count} voicings for {Intent}",
            results.Count, structured.ChordSymbol ?? structured.ModeName ?? "(tags)");

        return new AgentResponse
        {
            AgentId = $"skill.{Name.ToLowerInvariant()}",
            Result = sb.ToString(),
            Confidence = (float)Math.Clamp(results[0].Score, 0.0, 1.0),
            Evidence = evidence,
            Assumptions = [],
            Data = new
            {
                interpreted = structured,
                results = results.Select(r => new
                {
                    diagram = r.Document.Diagram,
                    chordName = r.Document.ChordName,
                    instrument = r.Document.VoicingType,
                    score = r.Score,
                }).ToList(),
            },
        };
    }

    // Chord-shape detector: uppercase root (no IgnoreCase) followed by an
    // accidental/quality immediately, or a *valid chord-extension digit*
    // (5,6,7,9,11,13). Matches "Cmaj7", "G7", "F#m", "Calt", "C7b9", "E5".
    // Case-sensitive root prevents false positives on bare lowercase "a" / "e"
    // in normal English ("show me a beginner shape"). Restricting the bare-digit
    // branch to real extensions (rather than any \d) rejects non-chord
    // letter+number tokens like "B12" (vitamin) and "A4" (paper size) that the
    // old \d branch matched — see ga#261. 4 (needs sus/add) and stray multi-digit
    // numbers no longer parse as chords.
    [GeneratedRegex(@"\b[A-G][#b]?(?:maj|min|m|M|dim|aug|sus|add|alt|°|Δ|11|13|5|6|7|9)\w*\b")]
    private static partial Regex ChordSuffixRegex();

    // Spaced quality form: "C major", "Bb minor", "F# augmented". Quality word
    // accepts upper- and lower-case first letters but requires the root to be
    // an uppercase chord letter.
    [GeneratedRegex(@"\b[A-G][#b]?\s+(?:[Mm]ajor|[Mm]inor|[Mm]aj|[Mm]in|[Dd]im|[Aa]ug|[Ss]us)\b")]
    private static partial Regex ChordWithSpacedQualityRegex();
}
