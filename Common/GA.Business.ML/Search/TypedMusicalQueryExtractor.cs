namespace GA.Business.ML.Search;

using GA.Domain.Services;

/// <summary>
///     Deterministic, zero-cost query extractor. Tokenizes the query and tries to match
///     each token against the chord parser, the mode vocabulary, and the symbolic tag
///     registry used by the corpus. Produces a populated <see cref="StructuredQuery"/>
///     when anything matches, or an empty one when nothing does — which the composite
///     extractor uses as the trigger for LLM fallback.
///
///     No regex, no LLM, runs in &lt; 5 ms. The common path for structured queries.
/// </summary>
public sealed class TypedMusicalQueryExtractor : IMusicalQueryExtractor
{
    private static readonly HashSet<string> KnownModesSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "ionian", "dorian", "phrygian", "lydian", "mixolydian", "aeolian", "locrian",
        "major", "minor",
        "harmonic-minor", "harmonic minor",
        "melodic-minor", "melodic minor",
        "pentatonic", "blues",
        "whole-tone", "whole tone",
        "diminished", "altered",
        "phrygian-dominant", "phrygian dominant",
        "lydian-dominant", "lydian dominant",
    };

    /// <summary>Canonical mode/scale names the typed parser recognizes.</summary>
    public static IReadOnlyCollection<string> KnownModes => KnownModesSet;

    private static readonly char[] TokenDelimiters =
        [' ', '\t', '\n', '\r', ',', ';', '.', '!', '?', '(', ')', '[', ']'];

    public Task<StructuredQuery> ExtractAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(new StructuredQuery(null, null, null, null, null));

        var tokens = query.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries);

        string? chordSymbol = null;
        int? root = null;
        int[]? pcs = null;
        string? modeName = null;
        var tags = new List<string>();

        var registry = SymbolicTagRegistry.Instance;

        for (var i = 0; i < tokens.Length; i++)
        {
            var tok = tokens[i];

            // 1. First chord we see wins — subsequent chords are ignored for now.
            //    Chord roots are uppercase by convention; this rules out words like
            //    "a"/"be"/"fade" that would otherwise parse as valid bare-letter chords.
            if (chordSymbol is null
                && tok.Length >= 1
                && char.IsUpper(tok[0])
                && ChordPitchClasses.TryParse(tok, out var r, out var p))
            {
                chordSymbol = tok;
                root = r;
                pcs = p;
                continue;
            }

            // 2. Mode: single-word modes or two-word ("harmonic minor").
            if (modeName is null)
            {
                if (KnownModesSet.Contains(tok))
                {
                    modeName = tok;
                    continue;
                }
                if (i + 1 < tokens.Length)
                {
                    var twoWord = tok + " " + tokens[i + 1];
                    if (KnownModesSet.Contains(twoWord))
                    {
                        modeName = twoWord;
                        continue;
                    }
                }
            }

            // 3. Symbolic tag — matches the corpus's vocabulary (case-insensitive,
            //    hyphen-normalized, with prefix/substring fallback). Require tokens
            //    ≥ 3 chars so the registry's substring-contains fallback doesn't fire
            //    on stop-words ("a", "me", "to") that happen to be substrings of a tag.
            if (tok.Length >= 3 && registry.GetBitIndex(tok).HasValue)
            {
                tags.Add(tok.ToLowerInvariant());
            }
        }

        var result = new StructuredQuery(
            ChordSymbol: chordSymbol,
            RootPitchClass: root,
            PitchClasses: pcs,
            ModeName: modeName,
            Tags: tags.Count == 0 ? null : tags);

        return Task.FromResult(result);
    }
}

/// <summary>
///     Two-tier extractor. The typed tier runs first (deterministic, ~1 ms); only when
///     the typed extractor produces nothing useful does the LLM tier run. This preserves
///     the pure-geometry retrieval path for structured queries and reserves cloud LLM
///     spend + latency for genuinely fuzzy intent.
/// </summary>
public sealed class CompositeMusicalQueryExtractor(
    TypedMusicalQueryExtractor typed,
    LlmMusicalQueryExtractor llm) : IMusicalQueryExtractor
{
    public async Task<StructuredQuery> ExtractAsync(string query, CancellationToken cancellationToken = default)
    {
        var fast = await typed.ExtractAsync(query, cancellationToken);
        if (HasContent(fast)) return fast;

        return await llm.ExtractAsync(query, cancellationToken);
    }

    private static bool HasContent(StructuredQuery q) =>
        q.ChordSymbol is not null
        || (q.PitchClasses is { Length: > 0 })
        || q.ModeName is not null
        || (q.Tags is { Count: > 0 });
}
