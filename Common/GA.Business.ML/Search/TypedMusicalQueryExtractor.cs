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

    /// <summary>
    ///     Instrument words → canonical filter value. Pulled out of the token stream so
    ///     "Dm bass" / "C ukulele" / "Cmaj7 on guitar" route to the index's instrument
    ///     filter instead of being treated as tags.
    /// </summary>
    private static readonly Dictionary<string, string> InstrumentAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["guitar"]  = "guitar",
        ["bass"]    = "bass",
        ["ukulele"] = "ukulele",
        ["uke"]     = "ukulele",
    };

    /// <summary>
    ///     Linguistic filler words that describe the kind of object being asked about
    ///     rather than a stylistic / structural property of the voicing. Dropping them
    ///     here stops the registry's substring fallback from hitting generic matches
    ///     ("chord" would otherwise fire on the first tag whose name contains "chord" —
    ///     e.g. "beatles-chord" — polluting the SYMBOLIC query vector). Keep this list
    ///     tight: only words with no standalone musical signal.
    /// </summary>
    private static readonly HashSet<string> TagStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "chord", "chords", "voicing", "voicings", "shape", "shapes",
        "music", "musical", "sound", "sounds",
        "play", "playing", "position",
    };

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
        string? instrument = null;
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

            // 3. Instrument filter — first hit wins. Consumes the token so it never
            //    leaks into the tag stream (where "bass" would otherwise miss the
            //    registry and silently return no voicings from the wrong population).
            if (instrument is null && InstrumentAliases.TryGetValue(tok, out var inst))
            {
                instrument = inst;
                continue;
            }

            // 4. Linguistic filler ("chord", "voicing", "shape", …) never becomes a tag.
            //    Without this, the registry's substring fallback maps "chord" onto the
            //    first tag whose name contains it and poisons the SYMBOLIC vector.
            if (TagStopWords.Contains(tok))
            {
                continue;
            }

            // 5. Symbolic tag — matches the corpus's vocabulary (case-insensitive,
            //    hyphen-normalized, with prefix/substring fallback). Require tokens
            //    ≥ 3 chars so the registry's substring-contains fallback doesn't fire
            //    on stop-words ("a", "me", "to") that happen to be substrings of a tag.
            if (tok.Length >= 3 && registry.GetBitIndex(tok).HasValue)
            {
                tags.Add(tok.ToLowerInvariant());
            }
        }

        // Two-word iconic-tag support ("hendrix chord", "james bond chord", …). The
        // single-token pass picks up "hendrix" alone when it matches the SymbolicTag
        // registry; but "james-bond-chord" only resolves when we try adjacent-pair and
        // triple composites. Cheap: O(n) pair scan + O(n) triple scan, both using the
        // same normalization as IconicChordsService.FindChordByTag.
        TryMatchIconicMultiWord(tokens, tags);

        // 6. Iconic-chord fallback anchor. If we collected a tag that corresponds to
        //    an entry in IconicChordsService *and* no chord symbol was typed, seed
        //    chord/PCs from that iconic chord. Turns "Hendrix chord" from a bare-tag
        //    score of ~0.06 into a real E7#9 retrieval, because now STRUCTURE +
        //    IDENTITY partitions carry signal alongside SYMBOLIC.
        if (chordSymbol is null && tags.Count > 0)
        {
            foreach (var t in tags)
            {
                var iconic = IconicChordsService.FindChordByTag(t);
                if (iconic is null || iconic.PitchClasses.Count == 0) continue;

                chordSymbol = iconic.TheoreticalName;
                pcs = [.. iconic.PitchClasses];
                root = iconic.PitchClasses.Count > 0 ? iconic.PitchClasses[0] : null;
                break;
            }
        }

        var result = new StructuredQuery(
            ChordSymbol: chordSymbol,
            RootPitchClass: root,
            PitchClasses: pcs,
            ModeName: modeName,
            Tags: tags.Count == 0 ? null : tags)
        {
            Instrument = instrument,
        };

        return Task.FromResult(result);
    }

    private static void TryMatchIconicMultiWord(string[] tokens, List<string> tags)
    {
        // Scan 2- and 3-token windows for iconic-chord tag names that don't reduce
        // to a single token match (e.g. "james bond chord" → "james-bond-chord").
        // Add the canonical hyphenated form to tags when the iconic service knows it.
        for (var i = 0; i < tokens.Length; i++)
        {
            for (var span = 2; span <= 3 && i + span <= tokens.Length; span++)
            {
                var composite = string.Join("-", tokens.Skip(i).Take(span)).ToLowerInvariant();
                if (tags.Contains(composite)) continue;

                var iconic = IconicChordsService.FindChordByTag(composite);
                if (iconic is not null)
                {
                    tags.Add(composite);
                }
            }
        }
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
