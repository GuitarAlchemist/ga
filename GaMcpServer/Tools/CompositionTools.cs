namespace GaMcpServer.Tools;

using System.ComponentModel;
using System.Text.Json;
using GA.Business.ML.Search;
using ModelContextProtocol.Server;

/// <summary>
///     MCP composition tools — progression generation from templates + voice-leading
///     pair retrieval across two chords. These wrap the existing theory primitives
///     (<see cref="ChordPitchClasses"/>, <see cref="OptickSearchStrategy"/>) into
///     composition-shaped entry points: "give me a progression" / "bridge chord A to
///     chord B with minimum voice motion."
///
///     Tools stay deliberately template-based and deterministic — probabilistic
///     generation belongs on the ix side (grammar evolve / Markov / bandit) and can
///     be wired through a future bridge tool if/when that path is validated.
/// </summary>
[McpServerToolType]
public static class CompositionTools
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Progression template library — (semitoneOffset, qualitySuffix) tuples.
    //  Offset is relative to the user-supplied root, so "C ii-V-I" yields
    //  Dm7 / G7 / Cmaj7 without any mode-inference machinery.
    // ═══════════════════════════════════════════════════════════════════════
    private record ProgressionStep(int SemitoneOffset, string Quality, string RomanLabel);

    private static readonly Dictionary<string, ProgressionStep[]> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        // Jazz staples
        ["ii-V-I"] =
        [
            new(2, "m7",  "ii7"),
            new(7, "7",   "V7"),
            new(0, "maj7", "Imaj7"),
        ],
        ["circle-of-fifths"] =
        [
            new(9, "m7",  "vi7"),
            new(2, "m7",  "ii7"),
            new(7, "7",   "V7"),
            new(0, "maj7", "Imaj7"),
        ],
        ["rhythm-changes-a"] =
        [
            new(0, "maj7", "Imaj7"),
            new(9, "m7",   "vi7"),
            new(2, "m7",   "ii7"),
            new(7, "7",    "V7"),
            new(4, "m7",   "iii7"),
            new(9, "m7",   "vi7"),
            new(2, "m7",   "ii7"),
            new(7, "7",    "V7"),
        ],

        // Pop / folk
        ["I-V-vi-IV"] =
        [
            new(0, "",  "I"),
            new(7, "",  "V"),
            new(9, "m", "vi"),
            new(5, "",  "IV"),
        ],
        ["I-vi-IV-V"] =
        [
            new(0, "",  "I"),
            new(9, "m", "vi"),
            new(5, "",  "IV"),
            new(7, "",  "V"),
        ],
        ["canon"] =
        [
            new(0, "",  "I"),
            new(7, "",  "V"),
            new(9, "m", "vi"),
            new(4, "m", "iii"),
            new(5, "",  "IV"),
            new(0, "",  "I"),
            new(5, "",  "IV"),
            new(7, "",  "V"),
        ],

        // Blues
        ["12-bar-blues"] =
        [
            new(0, "7", "I7"), new(0, "7", "I7"), new(0, "7", "I7"), new(0, "7", "I7"),
            new(5, "7", "IV7"), new(5, "7", "IV7"), new(0, "7", "I7"), new(0, "7", "I7"),
            new(7, "7", "V7"), new(5, "7", "IV7"), new(0, "7", "I7"), new(7, "7", "V7"),
        ],

        // Minor-key staples
        ["minor-vamp"] =
        [
            new(0, "m", "i"),
            new(5, "m", "iv"),
            new(7, "7", "V7"),  // harmonic-minor dominant
        ],
        ["andalusian"] =
        [
            new(0,  "m", "i"),
            new(10, "",  "bVII"),
            new(8,  "",  "bVI"),
            new(7,  "",  "V"),   // Phrygian cadence
        ],
    };

    [McpServerTool]
    [Description(
        "Generate a chord progression from a template in a given key. Templates: ii-V-I, " +
        "circle-of-fifths, rhythm-changes-a (first 8 bars), I-V-vi-IV (pop axis), " +
        "I-vi-IV-V (doo-wop), canon (Pachelbel 8-bar), 12-bar-blues, minor-vamp, " +
        "andalusian. Returns Roman numeral labels, concrete chord symbols, and pitch " +
        "classes. Deterministic — no LLM. Compose by chaining with ga_search_voicings " +
        "to realize each step.")]
    public static string GaGenerateProgression(
        [Description("Key root: C, C#/Db, D, D#/Eb, E, F, F#/Gb, G, G#/Ab, A, A#/Bb, B")]
        string root,
        [Description("Template name (see tool description) — case-insensitive")]
        string template,
        [Description("Optional length: truncates or loops the template. Omit for natural length.")]
        int? length = null)
    {
        if (string.IsNullOrWhiteSpace(root))
            return JsonSerializer.Serialize(new { error = "root is required" });
        if (string.IsNullOrWhiteSpace(template))
            return JsonSerializer.Serialize(new { error = "template is required" });

        if (!Templates.TryGetValue(template.Trim(), out var steps))
        {
            return JsonSerializer.Serialize(new
            {
                error = $"unknown template '{template}'",
                availableTemplates = Templates.Keys.OrderBy(k => k).ToArray(),
            });
        }

        // Validate root via ChordPitchClasses (single source of truth for root parsing).
        if (!ChordPitchClasses.TryParse(root.Trim(), out var rootPc, out _) || rootPc is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"unknown root '{root}' — try C, D, Eb, F#, etc.",
                knownRoots = ChordPitchClasses.KnownRoots.OrderBy(r => r).ToArray(),
            });
        }

        // Apply optional length adjustment (loop or truncate).
        var adjusted = length is int n && n > 0 && n != steps.Length
            ? Enumerable.Range(0, n).Select(i => steps[i % steps.Length]).ToArray()
            : steps;

        var flats = PrefersFlats(root);
        var chords = adjusted.Select(step =>
        {
            var pcs = PitchClassSetForDegree(rootPc.Value, step, flats);
            var symbol = BuildSymbol(rootPc.Value, step, flats);
            return new
            {
                roman = step.RomanLabel,
                symbol,
                degree = step.SemitoneOffset,
                quality = string.IsNullOrEmpty(step.Quality) ? "maj" : step.Quality,
                pitchClasses = pcs,
            };
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            root = root.Trim(),
            template = template.Trim(),
            length = chords.Length,
            chords,
            note = "Compose by passing each chord to ga_search_voicings; stitch with " +
                   "ga_voice_leading_pair for smooth-voiced transitions.",
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description(
        "Given two chord symbols, return voicing pairs (v1, v2) sorted by voice-leading " +
        "distance — smallest total semitone motion first. Uses a greedy sorted-pitch " +
        "matching (O(n·m) per candidate pair) — good enough for retrieval ranking, not " +
        "a formal Hungarian-optimal assignment. Retrieves top candidates for each chord " +
        "from the OPTIC-K index, so pairs are constrained to playable voicings.")]
    public static async Task<string> GaVoiceLeadingPair(
        [Description("Source chord symbol (e.g. 'Dm7', 'Cmaj7')")]
        string fromChord,
        [Description("Target chord symbol (e.g. 'G7', 'Fmaj7')")]
        string toChord,
        [Description("Max pairs to return (default 5, capped 20)")]
        int limit = 5,
        [Description("Optional instrument filter: guitar | bass | ukulele")]
        string? instrument = null,
        [Description("Candidates per chord to search across (default 15, capped 50). Higher = more exhaustive pair search, slower.")]
        int candidatesPerChord = 15,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromChord) || string.IsNullOrWhiteSpace(toChord))
            return JsonSerializer.Serialize(new { error = "fromChord and toChord are required" });

        limit = Math.Clamp(limit, 1, 20);
        candidatesPerChord = Math.Clamp(candidatesPerChord, 3, 50);

        // Use the same pipeline the search tool uses so candidate quality matches what
        // ga_search_voicings would return directly.
        var fromResult = await SearchByChordAsync(fromChord, candidatesPerChord, instrument, cancellationToken);
        if (fromResult.Error is not null)
            return JsonSerializer.Serialize(new { error = $"fromChord: {fromResult.Error}" });
        var toResult = await SearchByChordAsync(toChord, candidatesPerChord, instrument, cancellationToken);
        if (toResult.Error is not null)
            return JsonSerializer.Serialize(new { error = $"toChord: {toResult.Error}" });

        if (fromResult.Hits.Count == 0 || toResult.Hits.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = "no voicings retrieved for one or both chords",
                fromHits = fromResult.Hits.Count,
                toHits = toResult.Hits.Count,
            });
        }

        // Greedy pair enumeration with voice-leading distance.
        var pairs = new List<(VoicingSearchResult From, VoicingSearchResult To, double Distance)>(
            fromResult.Hits.Count * toResult.Hits.Count);

        foreach (var v1 in fromResult.Hits)
            foreach (var v2 in toResult.Hits)
                pairs.Add((v1, v2, VoiceLeadingDistance(v1.Document.MidiNotes, v2.Document.MidiNotes)));

        var top = pairs
            .OrderBy(p => p.Distance)
            .ThenByDescending(p => p.From.Score + p.To.Score)  // tie-break on retrieval quality
            .Take(limit)
            .Select(p => new
            {
                distance = Math.Round(p.Distance, 2),
                from = new
                {
                    diagram = p.From.Document.Diagram,
                    chordName = p.From.Document.ChordName,
                    instrument = p.From.Document.VoicingType,
                    midiNotes = p.From.Document.MidiNotes,
                    score = Math.Round(p.From.Score, 4),
                },
                to = new
                {
                    diagram = p.To.Document.Diagram,
                    chordName = p.To.Document.ChordName,
                    instrument = p.To.Document.VoicingType,
                    midiNotes = p.To.Document.MidiNotes,
                    score = Math.Round(p.To.Score, 4),
                },
            })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            fromChord,
            toChord,
            instrument,
            pairCount = top.Length,
            totalCandidatesEvaluated = fromResult.Hits.Count * toResult.Hits.Count,
            pairs = top,
            note = "distance = sum of absolute semitone differences under greedy ascending-pitch matching.",
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private record SearchOutcome(List<VoicingSearchResult> Hits, string? Error);

    private static readonly Lazy<MusicalQueryEncoder> EncoderShared = new(() =>
        new MusicalQueryEncoder(
            new GA.Business.ML.Embeddings.Services.TheoryVectorService(),
            new GA.Business.ML.Embeddings.Services.ModalVectorService(),
            new GA.Business.ML.Embeddings.Services.SymbolicVectorService(),
            new GA.Business.ML.Embeddings.Services.RootVectorService()));

    private static async Task<SearchOutcome> SearchByChordAsync(
        string chordSymbol, int limit, string? instrument, CancellationToken ct)
    {
        if (!ChordPitchClasses.TryParse(chordSymbol.Trim(), out var rootPc, out var pcs) || rootPc is null)
            return new([], $"unrecognized chord symbol '{chordSymbol}'");

        var structured = new StructuredQuery(
            ChordSymbol: chordSymbol.Trim(),
            RootPitchClass: rootPc,
            PitchClasses: pcs,
            ModeName: null,
            Tags: null);

        var vec = EncoderShared.Value.Encode(structured);
        var strategy = GetStrategy();
        var hits = string.IsNullOrWhiteSpace(instrument)
            ? await strategy.SemanticSearchAsync(vec, limit, ct)
            : await strategy.HybridSearchAsync(vec, new VoicingSearchFilters(VoicingType: instrument), limit, ct);

        return new(hits, null);
    }

    private static OptickSearchStrategy GetStrategy()
    {
        // Reach through VoicingSearchTool's cached strategy via reflection of the
        // non-public static field. Cheap, happens once per process; avoids duplicating
        // the index-path walker here.
        var field = typeof(VoicingSearchTool).GetField("Strategy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("VoicingSearchTool.Strategy not found");
        var lazy = (Lazy<OptickSearchStrategy>)field.GetValue(null)!;
        return lazy.Value;
    }

    /// <summary>
    ///     Sum of absolute semitone differences under greedy ascending-pitch matching.
    ///     For voicings of unequal note count, the shorter one pairs against its nearest
    ///     neighbors and the remainder contributes a small per-note penalty (simulating
    ///     "voice appears from silence" / "voice disappears"). This is not a formal
    ///     Hungarian-optimal assignment — it's O(n log n) and produces a ranking good
    ///     enough to surface smooth voice-leadings among candidates.
    /// </summary>
    private static double VoiceLeadingDistance(IReadOnlyList<int> midi1, IReadOnlyList<int> midi2)
    {
        if (midi1.Count == 0 || midi2.Count == 0) return double.MaxValue;

        var a = midi1.OrderBy(n => n).ToArray();
        var b = midi2.OrderBy(n => n).ToArray();
        var common = Math.Min(a.Length, b.Length);

        double sum = 0;
        for (var i = 0; i < common; i++)
            sum += Math.Abs(a[i] - b[i]);

        // Penalty for unmatched voices (extra notes on either side).
        var extras = Math.Abs(a.Length - b.Length);
        sum += extras * 3.0;  // soft penalty — 3 semitones per orphan note.
        return sum;
    }

    private static int[] PitchClassSetForDegree(int rootPc, ProgressionStep step, bool preferFlats)
    {
        var symbol = BuildSymbol(rootPc, step, preferFlats);
        return ChordPitchClasses.TryParse(symbol, out _, out var pcs) ? pcs : [];
    }

    private static string BuildSymbol(int rootPc, ProgressionStep step, bool preferFlats)
    {
        var chordRootPc = (rootPc + step.SemitoneOffset) % 12;
        var rootName = preferFlats
            ? PitchClassToFlatName(chordRootPc)
            : PitchClassToSharpName(chordRootPc);
        return rootName + step.Quality;
    }

    private static string PitchClassToSharpName(int pc) => pc switch
    {
        0  => "C",  1  => "C#", 2  => "D",  3  => "D#",
        4  => "E",  5  => "F",  6  => "F#", 7  => "G",
        8  => "G#", 9  => "A",  10 => "A#", 11 => "B",
        _  => "C",
    };

    private static string PitchClassToFlatName(int pc) => pc switch
    {
        0  => "C",  1  => "Db", 2  => "D",  3  => "Eb",
        4  => "E",  5  => "F",  6  => "Gb", 7  => "G",
        8  => "Ab", 9  => "A",  10 => "Bb", 11 => "B",
        _  => "C",
    };

    /// <summary>
    ///     True when the user's key root is on the flat side of the circle of fifths
    ///     (F, Bb, Eb, Ab, Db, Gb — i.e. the symbol contains a 'b' OR is plain F). Used
    ///     to pick flat vs sharp enharmonic spelling for generated chord symbols so
    ///     "ii-V-I in Bb" yields Bbmaj7 rather than the sharp-spelled A#maj7.
    ///     C is treated as sharp-side (the pop convention for chromatic chords in C).
    /// </summary>
    private static bool PrefersFlats(string root)
    {
        var r = root.Trim();
        return r.Contains('b') || r.Equals("F", StringComparison.OrdinalIgnoreCase);
    }
}
