namespace GA.Business.ML.Agents;

using System.Text;
using GA.Business.ML.Search;
using Microsoft.Extensions.AI;

/// <summary>
///     Agent specialized in retrieving guitar chord voicings by musical structure.
///     Owns the full OPTIC-K pipeline: the composite extractor parses chord/mode/tags from
///     the query, <see cref="MusicalQueryEncoder"/> composes a 112-dim compact vector in the
///     same semantic space as the on-disk corpus, and the injected search service runs the
///     dot-product scan via <see cref="OptickSearchStrategy"/>.
///     <para>
///         Unlike the other domain agents that reason primarily via LLM, this agent's value
///         is *retrieval grounding*: it returns a structured list of voicings with diagrams
///         and scores that downstream agents (or a final chat-composition step) can cite.
///     </para>
/// </summary>
public sealed class VoicingAgent(
    IChatClient chatClient,
    ILogger<VoicingAgent> logger,
    EnhancedVoicingSearchService voicingSearch,
    IMusicalQueryExtractor extractor,
    MusicalQueryEncoder encoder)
    : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.Voicing;
    public override string Name => "Voicing Agent";

    public override string Description =>
        "Finds playable guitar chord voicings matching a named chord, mode, or style. " +
        "Returns specific fingerings with diagrams (e.g. x-3-2-0-1-0) ranked by musical " +
        "similarity over partition-weighted OPTIC-K geometry, not text embeddings. " +
        "Use for queries like 'Cmaj7 drop2 jazz', 'F# Lydian shapes', 'rootless Dm7'.";

    public override IReadOnlyList<string> Capabilities =>
    [
        "Voicing retrieval by chord",
        "Voicing retrieval by mode/scale",
        "Voicing retrieval by style tag (jazz, blues, rock)",
        "Voicing retrieval by technique tag (drop2, drop3, shell, rootless, quartal, barre)",
        "Per-instrument filtering (guitar, bass, ukulele)",
    ];

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("VoicingAgent processing: {Query}", request.Query);

        // Extract intent deterministically when possible; LLM fallback kicks in only for
        // fuzzy queries (composite extractor decides internally).
        var structured = await extractor.ExtractAsync(request.Query, cancellationToken);
        var hasIntent = structured.ChordSymbol is not null
                        || (structured.PitchClasses is { Length: > 0 })
                        || structured.ModeName is not null
                        || (structured.Tags is { Count: > 0 });

        if (!hasIntent)
        {
            return new AgentResponse
            {
                AgentId = AgentId,
                Result = "I couldn't find a chord name, mode, or style tag in your request. " +
                         "Try naming a chord (e.g. 'Cmaj7'), a mode ('F# Lydian'), or a technique " +
                         "('drop2', 'shell', 'rootless').",
                Confidence = 0.2f,
                Evidence = [],
                Assumptions = ["Query contained no musical entities recognizable by the typed parser."]
            };
        }

        var limit = request.Metadata is { } md
                    && md.TryGetValue("limit", out var limitRaw)
                    && limitRaw is int i && i > 0
            ? Math.Min(i, 50)
            : 10;

        var queryVector = encoder.Encode(structured);

        Task<double[]> Generator(string _) => Task.FromResult(queryVector);
        var results = await voicingSearch.SearchAsync(
            request.Query, Generator, limit, filters: null, cancellationToken);

        if (results.Count == 0)
        {
            return new AgentResponse
            {
                AgentId = AgentId,
                Result = "The OPTIC-K index returned no matches for this query.",
                Confidence = 0.3f,
                Evidence = [],
                Assumptions = ["Corpus does not contain voicings matching this musical profile."],
                Data = new { interpreted = structured }
            };
        }

        var evidence = results
            .Take(Math.Min(results.Count, 5))
            .Select(r => $"{r.Document.ChordName ?? "?"} · {r.Document.Diagram} · score={r.Score:F4}")
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} voicing{(results.Count == 1 ? "" : "s")} " +
                       $"matching {DescribeIntent(structured)}:");
        sb.AppendLine();
        foreach (var r in results)
        {
            sb.AppendLine($"- **{r.Document.ChordName ?? "Voicing"}** `{r.Document.Diagram}` " +
                          $"({r.Document.VoicingType ?? "guitar"}, score {r.Score:F3})");
        }

        // Top-score proxy for confidence. Partition-weighted cosine maxes around 1.0 for a
        // true musical match; 0.5+ is a strong hit on STRUCTURE alone.
        var confidence = (float)Math.Clamp(results[0].Score, 0.0, 1.0);

        return new AgentResponse
        {
            AgentId = AgentId,
            Result = sb.ToString(),
            Confidence = confidence,
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
                    midiNotes = r.Document.MidiNotes,
                    pitchClasses = r.Document.PitchClasses,
                    score = r.Score
                }).ToList()
            }
        };
    }

    private static string DescribeIntent(StructuredQuery q)
    {
        var parts = new List<string>();
        if (q.ChordSymbol is not null) parts.Add($"chord {q.ChordSymbol}");
        if (q.ModeName is not null) parts.Add($"mode {q.ModeName}");
        if (q.Tags is { Count: > 0 }) parts.Add($"tags [{string.Join(", ", q.Tags)}]");
        return parts.Count == 0 ? "your query" : string.Join(" + ", parts);
    }
}
