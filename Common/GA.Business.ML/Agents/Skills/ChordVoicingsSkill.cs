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
        var hasVoicingIntent = VoicingKeywords.Any(k => q.Contains(k, StringComparison.Ordinal));
        if (!hasVoicingIntent) return false;
        // Guard: at least one chord-shaped token (letter + optional accidental
        // + optional quality). Avoids matching "voicing" alone.
        return ChordTokenRegex().IsMatch(message);
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var structured = await extractor.ExtractAsync(message, cancellationToken);
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
        var results = await voicingSearch.SearchAsync(message, Generator, topK: 8, filters, cancellationToken);

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

    // Chord-shape detector: letter + optional accidental + optional
    // quality suffix. Conservative — only matches what looks like a chord.
    [GeneratedRegex(@"\b[A-G][#b]?(maj|min|m|M|dim|aug|sus|add|alt|°|Δ)?\d*\b", RegexOptions.IgnoreCase)]
    private static partial Regex ChordTokenRegex();
}
