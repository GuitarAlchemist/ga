namespace GaMcpServer.Tools;

using System.ComponentModel;
using System.Text.Json;
using GA.Business.ML.Search;
using GA.Domain.Services;
using ModelContextProtocol.Server;

/// <summary>
///     Returns the canonical vocabulary that <see cref="VoicingSearchTool.GaSearchVoicings"/>
///     accepts: mode names, chord-quality suffixes, root letters, and symbolic style/technique
///     tags. Intended to be called ONCE per Claude Code session — the client caches the result
///     and uses it to canonicalize user queries before calling the search tool.
///
///     This replaces what would otherwise be a per-query LLM-sampling round-trip: instead of
///     the server asking Claude to extract intent, Claude extracts intent client-side against
///     the server-published vocabulary, and calls search with canonical tokens. Retrieval stays
///     deterministic and sub-50ms; fuzzy queries become the LLM's *pre-formulation* problem,
///     which it's already doing anyway.
/// </summary>
[McpServerToolType]
public static class VoicingVocabularyTool
{
    [McpServerTool]
    [Description(
        "Returns the canonical vocabulary the voicing-search tool accepts: modes, chord " +
        "qualities, roots, and style/technique tags. Call this once per session when the " +
        "user's query uses descriptive language ('warm', 'sparkly', 'moody'). Map their " +
        "words to the returned tags (e.g. 'warm' → 'jazz' or 'mellow' if listed), then " +
        "call ga_search_voicings with canonical tokens. Example: user says 'something " +
        "jazzy in F minor' → canonical query 'Fm jazz'.")]
    public static string GaVoicingVocabulary()
    {
        var tags = SymbolicTagRegistry.Instance.GetAllKnownTags().OrderBy(t => t).ToList();

        return JsonSerializer.Serialize(new
        {
            description =
                "Canonical vocabulary accepted by ga_search_voicings. Construct the query " +
                "string by concatenating a root letter + optional quality suffix (e.g. 'Cmaj7', " +
                "'F#m7b5') plus any mode name and/or known tags, space-separated.",

            roots = ChordPitchClasses.KnownRoots.OrderBy(r => r).ToList(),

            chordQualitySuffixes = ChordPitchClasses.KnownQualities
                .Where(q => q.Length > 0)
                .OrderBy(q => q)
                .ToList(),

            emptyQualityMeansMajorTriad = true,

            modes = TypedMusicalQueryExtractor.KnownModes.OrderBy(m => m).ToList(),

            symbolicTags = tags,

            symbolicTagPartialMatch =
                "Matching is case-insensitive, hyphen-normalized (spaces and underscores " +
                "become hyphens), and supports substring fallback — 'sweep' will match " +
                "'sweep-picking' if that's the canonical tag.",

            queryConstructionExamples = new[]
            {
                new { userSaid = "Cmaj7 jazz voicing",              canonicalQuery = "Cmaj7 jazz" },
                new { userSaid = "F# Lydian drop 2",                 canonicalQuery = "F# Lydian drop2" },
                new { userSaid = "rootless Dm7",                     canonicalQuery = "Dm7 rootless" },
                new { userSaid = "something jazzy in F minor",       canonicalQuery = "Fm jazz" },
                new { userSaid = "shell voicing for G7",             canonicalQuery = "G7 shell" },
            },

            whenVocabularyMisses =
                "If the user's descriptive word (e.g. 'warm', 'sparkly', 'dark') doesn't " +
                "map to any listed tag, omit it from the query and prefer the chord/mode " +
                "anchor alone. The musical geometry will still rank voicings meaningfully " +
                "when STRUCTURE and/or MODAL partitions are populated."
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
