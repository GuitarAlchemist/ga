namespace GaMcpServer.Tools;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Serialization;

using GaReg = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// MCP tools that solve practical guitarist problems: key detection from a chord
/// progression, progression completion suggestions, arpeggio/mode pairing, and
/// easier voicing alternatives for barre-heavy chords.
/// </summary>

// ── Shared helpers ─────────────────────────────────────────────────────────────

file static class GuitaristHelpers
{
    // Enharmonic note-to-semitone map (covers sharps, flats, double-accidentals).
    private static readonly IReadOnlyDictionary<string, int> NoteToSemitone =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"]  = 0,  ["C#"] = 1,  ["Db"] = 1,
            ["D"]  = 2,  ["D#"] = 3,  ["Eb"] = 3,
            ["E"]  = 4,  ["Fb"] = 4,  ["E#"] = 5,
            ["F"]  = 5,  ["F#"] = 6,  ["Gb"] = 6,
            ["G"]  = 7,  ["G#"] = 8,  ["Ab"] = 8,
            ["A"]  = 9,  ["A#"] = 10, ["Bb"] = 10,
            ["B"]  = 11, ["Cb"] = 11, ["B#"] = 0,
        };

    // Major scale semitone offsets from root (Ionian).
    internal static readonly int[] MajorOffsets = [0, 2, 4, 5, 7, 9, 11];

    // Natural-minor scale semitone offsets from root (Aeolian).
    internal static readonly int[] MinorOffsets = [0, 2, 3, 5, 7, 8, 10];

    // Quality suffixes used when building diatonic chord symbols.
    internal static readonly (int Offset, string Suffix)[] MajorPattern =
        [(0, ""), (2, "m"), (4, "m"), (5, ""), (7, ""), (9, "m"), (11, "dim")];

    internal static readonly (int Offset, string Suffix)[] MinorPattern =
        [(0, "m"), (2, "dim"), (3, ""), (5, "m"), (7, "m"), (8, ""), (10, "")];

    internal static readonly string[] MajorRomans = ["I", "ii", "iii", "IV", "V", "vi", "vii°"];
    internal static readonly string[] MinorRomans = ["i", "ii°", "III", "iv", "v", "VI", "VII"];

    // Conventional flat-preferred note names for key naming.
    private static readonly string[] FlatNames =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static readonly string[] SharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>Parse a chord root token to a pitch class (0-11). Returns -1 on failure.</summary>
    internal static int ChordRootPc(string chordSymbol)
    {
        if (string.IsNullOrWhiteSpace(chordSymbol)) return -1;
        // Try two-character prefix first (e.g. "Bb", "C#"), then single character.
        if (chordSymbol.Length >= 2 && NoteToSemitone.TryGetValue(chordSymbol[..2], out var v2)) return v2;
        if (NoteToSemitone.TryGetValue(chordSymbol[..1], out var v1)) return v1;
        return -1;
    }

    /// <summary>
    /// Score how many of the given pitch-classes are diatonic to the key rooted at
    /// <paramref name="keyPc"/> using <paramref name="offsets"/>.
    /// </summary>
    internal static int ScoreKey(int keyPc, int[] offsets, IEnumerable<int> chordPcs)
    {
        var diatonic = offsets.Select(o => (keyPc + o) % 12).ToHashSet();
        return chordPcs.Count(pc => diatonic.Contains(pc));
    }

    /// <summary>Return a human-friendly key name, preferring flat spelling for flat keys.</summary>
    internal static string KeyName(int pc) =>
        pc switch
        {
            1 or 3 or 8 or 10 => FlatNames[pc],  // Db, Eb, Ab, Bb
            _                  => SharpNames[pc],
        };

    /// <summary>
    /// Build all 7 diatonic chord symbols for a key, using the sharp/flat spelling that
    /// matches the key root.
    /// </summary>
    internal static string[] DiatonicSymbols(int keyPc, bool useFlat, (int Offset, string Suffix)[] pattern)
    {
        var names = useFlat ? FlatNames : SharpNames;
        return [.. pattern.Select(p => names[(keyPc + p.Offset) % 12] + p.Suffix)];
    }

    /// <summary>Whether this key root conventionally uses flats (Bb, Eb, Ab, Db, Gb, F).</summary>
    internal static bool PreferFlat(int rootPc) => rootPc is 5 or 10 or 3 or 8 or 1 or 6;

    // ── DSL closure bridge ───────────────────────────────────────────────────────

    internal static async Task<string> InvokeAsync(
        string closureName,
        params (string Key, object Value)[] inputs)
    {
        try
        {
            var map = MapModule.OfSeq(inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));
            var result = await FSharpAsync.StartAsTask(
                GaReg.Global.Invoke(closureName, map),
                FSharpOption<TaskCreationOptions>.None,
                FSharpOption<CancellationToken>.None);
            return result.IsOk
                ? FormatResult(result.ResultValue)
                : $"Error: {result.ErrorValue}";
        }
        catch (Exception ex)
        {
            return $"Exception in {closureName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    internal static async Task<string[]> InvokeArrayAsync(
        string closureName,
        params (string Key, object Value)[] inputs)
    {
        try
        {
            var map = MapModule.OfSeq(inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));
            var result = await FSharpAsync.StartAsTask(
                GaReg.Global.Invoke(closureName, map),
                FSharpOption<TaskCreationOptions>.None,
                FSharpOption<CancellationToken>.None);
            if (!result.IsOk) return [];
            return result.ResultValue switch
            {
                string[] arr => arr,
                object[] arr => [.. arr.Select(x => x?.ToString() ?? "")],
                _ => []
            };
        }
        catch
        {
            return [];
        }
    }

    private static string FormatResult(object? value) =>
        value switch
        {
            string[] arr => string.Join(", ", arr),
            object[] arr => string.Join(", ", arr.Select(x => x?.ToString() ?? "")),
            null         => "(no result)",
            _            => value.ToString() ?? "(no result)"
        };
}

// ── Tool 1: GaKeyFromProgression ───────────────────────────────────────────────

[McpServerToolType]
public static class GaKeyFromProgressionTool
{
    [McpServerTool]
    [Description(
        "Detect the musical key from a chord progression. " +
        "Pass the chords as an array (e.g. [\"Am\",\"F\",\"C\",\"G\"]). " +
        "Returns the top 3 key candidates with confidence scores and matching chord lists. " +
        "Example: Am F C G → best guess C major (4/4 chords diatonic, 100%).")]
    public static string GaKeyFromProgression(
        [Description("Array of chord symbols in the progression, e.g. [\"Am\",\"F\",\"C\",\"G\"]")]
        string[] chords)
    {
        if (chords is not { Length: > 0 })
            return JsonSerializer.Serialize(new { error = "No chords provided." });

        // Parse each chord to its root pitch class; ignore unrecognised symbols.
        var chordPcs = chords
            .Select(c => (chord: c, pc: GuitaristHelpers.ChordRootPc(c)))
            .Where(x => x.pc >= 0)
            .ToList();

        if (chordPcs.Count == 0)
            return JsonSerializer.Serialize(new { error = "Could not parse any chord roots." });

        var pcs = chordPcs.Select(x => x.pc).ToList();

        // Score all 24 keys (12 major + 12 minor).
        var keyConfigs = new[]
        {
            (mode: "major", offsets: GuitaristHelpers.MajorOffsets, pattern: GuitaristHelpers.MajorPattern),
            (mode: "minor", offsets: GuitaristHelpers.MinorOffsets, pattern: GuitaristHelpers.MinorPattern)
        };

        var candidates = (
            from rootPc in Enumerable.Range(0, 12)
            from cfg in keyConfigs
            let diatonic = cfg.offsets.Select(o => (rootPc + o) % 12).ToHashSet()
            let matchingChords = chords.Where(c =>
            {
                var pc = GuitaristHelpers.ChordRootPc(c);
                return pc >= 0 && diatonic.Contains(pc);
            }).ToList()
            let score = matchingChords.Count
            where score > 0
            orderby score descending, (rootPc == pcs[0] ? 1 : 0) descending
            select new
            {
                key            = GuitaristHelpers.KeyName(rootPc) + " " + cfg.mode,
                mode           = cfg.mode,
                confidence     = $"{score}/{chords.Length} ({score * 100 / chords.Length}%)",
                matchingChords
            }
        ).Take(3).ToList();

        var best = candidates.FirstOrDefault();
        var result = new
        {
            candidates,
            bestGuess = best?.key ?? "unknown"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}

// ── Tool 2: GaProgressionCompletion ───────────────────────────────────────────

[McpServerToolType]
public static class GaProgressionCompletionTool
{
    // Harmonic function templates indexed by Roman numeral of the last input chord.
    // Each entry lists plausible continuations as Roman numeral arrays.
    private static readonly IReadOnlyDictionary<string, string[][]> MajorCompletions =
        new Dictionary<string, string[][]>(StringComparer.OrdinalIgnoreCase)
        {
            ["I"]    = [["IV", "V"], ["vi", "IV"], ["ii", "V"]],
            ["ii"]   = [["V", "I"], ["V7", "I"], ["IV", "I"]],
            ["iii"]  = [["IV", "V"], ["vi", "ii"], ["IV", "I"]],
            ["IV"]   = [["V", "I"], ["I", "V"], ["ii", "V"]],
            ["V"]    = [["I"], ["vi"], ["I", "vi"]],
            ["vi"]   = [["IV", "V"], ["ii", "V"], ["IV", "I"]],
            ["vii°"] = [["I"], ["I", "V"], ["vi"]],
        };

    private static readonly IReadOnlyDictionary<string, string[][]> MinorCompletions =
        new Dictionary<string, string[][]>(StringComparer.OrdinalIgnoreCase)
        {
            ["i"]    = [["iv", "v"], ["VI", "VII"], ["ii°", "v"]],
            ["ii°"]  = [["v", "i"], ["V", "i"], ["iv", "i"]],
            ["III"]  = [["VI", "VII"], ["iv", "v"], ["VII", "i"]],
            ["iv"]   = [["v", "i"], ["i", "v"], ["ii°", "v"]],
            ["v"]    = [["i"], ["VI"], ["i", "VI"]],
            ["VI"]   = [["VII", "i"], ["iv", "v"], ["iv", "i"]],
            ["VII"]  = [["i"], ["III"], ["i", "iv"]],
        };

    [McpServerTool]
    [Description(
        "Suggest 2-3 natural harmonic completions for an unfinished chord progression. " +
        "Detects the key automatically and uses diatonic function (ii-V-I, IV-V-I, etc.) to propose next chords. " +
        "Optional style hint: 'jazz', 'pop', 'blues', or 'classical'. " +
        "Example: [\"C\",\"Am\",\"F\"] → suggests G (V), G7 (V7), or Dm G (ii V).")]
    public static async Task<string> GaProgressionCompletion(
        [Description("Chord symbols already in the progression, e.g. [\"C\",\"Am\",\"F\"]")]
        string[] chords,
        [Description("Optional style: 'jazz', 'pop', 'blues', or 'classical'. Leave empty for generic.")]
        string style = "")
    {
        if (chords is not { Length: > 0 })
            return JsonSerializer.Serialize(new { error = "No chords provided." });

        // Step 1 — detect key via domain.analyzeProgression.
        var analysisRaw = await GuitaristHelpers.InvokeAsync(
            "domain.analyzeProgression",
            ("chords", string.Join(" ", chords)));

        // analysisRaw format: "Key: C major  (confidence 4/4)\nG      Am     F      C    \nI      vi     IV     I    "
        string detectedKey  = "C";
        string detectedMode = "major";
        string[] romanNumerals = [];

        var lines = analysisRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length >= 1 && lines[0].StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
        {
            var keyParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (keyParts.Length >= 3)
            {
                detectedKey  = keyParts[1];
                detectedMode = keyParts[2];
            }
        }
        if (lines.Length >= 3)
        {
            romanNumerals = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        // Step 2 — find Roman numeral of the last chord.
        var lastRoman = romanNumerals.LastOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? "I";

        // Step 3 — look up harmonic completions.
        var completionTable = detectedMode.Equals("minor", StringComparison.OrdinalIgnoreCase)
            ? MinorCompletions
            : MajorCompletions;

        if (!completionTable.TryGetValue(lastRoman, out var templates))
            templates = [["V", "I"], ["IV", "I"], ["I"]];

        // Step 4 — resolve Roman numerals to actual chord symbols in the detected key.
        var keyPc = GuitaristHelpers.ChordRootPc(detectedKey);
        if (keyPc < 0) keyPc = 0;

        var useFlat = GuitaristHelpers.PreferFlat(keyPc);
        var pattern = detectedMode.Equals("minor", StringComparison.OrdinalIgnoreCase)
            ? GuitaristHelpers.MinorPattern
            : GuitaristHelpers.MajorPattern;
        var romans  = detectedMode.Equals("minor", StringComparison.OrdinalIgnoreCase)
            ? GuitaristHelpers.MinorRomans
            : GuitaristHelpers.MajorRomans;
        var diatonic = GuitaristHelpers.DiatonicSymbols(keyPc, useFlat, pattern);

        string ResolveRoman(string roman)
        {
            // Match case-insensitively; strip ° for lookup
            var clean = roman.TrimEnd('°').TrimEnd('7');
            var idx   = Array.FindIndex(romans,
                r => r.Equals(roman, StringComparison.OrdinalIgnoreCase) ||
                     r.TrimEnd('°').Equals(clean, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return roman; // fallback: return as-is
            var sym = diatonic[idx];
            // If the roman had a "7" suffix, append it.
            return roman.EndsWith('7') ? sym + "7" : sym;
        }

        var styleTag = style.ToLowerInvariant() switch
        {
            "jazz"      => "jazz (may use 7th chords and ii-V-I cadences)",
            "pop"       => "pop (prefers simple triads and I-V-vi-IV patterns)",
            "blues"     => "blues (dominant 7th chords, IV-I turnarounds)",
            "classical" => "classical (voice-leading rules, IV-V-I cadences)",
            _           => "general"
        };

        var completions = templates.Take(3).Select((template, i) =>
        {
            var resolved  = template.Select(ResolveRoman).ToArray();
            var function_ = string.Join("-", template);
            var rationale = BuildRationale(function_, detectedKey, detectedMode, styleTag);
            return new
            {
                chords    = resolved,
                function  = function_,
                rationale
            };
        }).ToList();

        var result = new
        {
            key         = $"{detectedKey} {detectedMode}",
            style       = styleTag,
            lastChord   = lastRoman,
            completions
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildRationale(string func, string key, string mode, string style) =>
        func switch
        {
            "V-I"    => $"Perfect authentic cadence — the strongest resolution in {key} {mode}.",
            "V7-I"   => $"Dominant 7th cadence — adds harmonic tension before resolving to {key}.",
            "IV-V-I" => "Plagal approach leading to a perfect cadence.",
            "ii-V-I" => "Classic ii-V-I cadence — cornerstone of jazz and common-practice harmony.",
            "V-vi"   => "Deceptive cadence — avoids the expected I, adds surprise.",
            "IV-I"   => "Plagal cadence — softer resolution sometimes called the 'Amen' cadence.",
            "VII-i"  => $"Subtonic resolution to {key} {mode} tonic (common in rock and modal music).",
            _        => $"Harmonically logical continuation in {key} {mode} ({style})."
        };
}

// ── Tool 3: GaArpeggioSuggestions ─────────────────────────────────────────────

[McpServerToolType]
public static class GaArpeggioSuggestionsTool
{
    // Arpeggio and mode pairing by scale degree (0-based index into major/minor pattern).
    private static readonly (string Arpeggio, string Mode, string Notes)[] MajorDegreeModes =
    [
        ("maj7",  "Ionian (major)",     "R, M2, M3, P4, P5, M6, M7"),
        ("m7",    "Dorian",             "R, M2, m3, P4, P5, M6, m7"),
        ("m7",    "Phrygian",           "R, m2, m3, P4, P5, m6, m7"),
        ("maj7",  "Lydian",             "R, M2, M3, A4, P5, M6, M7"),
        ("7",     "Mixolydian",         "R, M2, M3, P4, P5, M6, m7"),
        ("m7",    "Aeolian (minor)",    "R, M2, m3, P4, P5, m6, m7"),
        ("m7b5",  "Locrian",            "R, m2, m3, P4, d5, m6, m7"),
    ];

    private static readonly (string Arpeggio, string Mode, string Notes)[] MinorDegreeModes =
    [
        ("m7",    "Aeolian (minor)",    "R, M2, m3, P4, P5, m6, m7"),
        ("m7b5",  "Locrian",            "R, m2, m3, P4, d5, m6, m7"),
        ("maj7",  "Ionian (major)",     "R, M2, M3, P4, P5, M6, M7"),
        ("m7",    "Dorian",             "R, M2, m3, P4, P5, M6, m7"),
        ("m7",    "Phrygian",           "R, m2, m3, P4, P5, m6, m7"),
        ("maj7",  "Lydian",             "R, M2, M3, A4, P5, M6, M7"),
        ("7",     "Mixolydian",         "R, M2, M3, P4, P5, M6, m7"),
    ];

    [McpServerTool]
    [Description(
        "For each chord in a progression, suggest the matching arpeggio and mode to improvise over it. " +
        "Optionally specify the key; otherwise the key is inferred from the progression. " +
        "Example: [\"Am\",\"F\",\"C\",\"G\"] in C major → Am: Aeolian/Am7, F: Lydian/Fmaj7, C: Ionian/Cmaj7, G: Mixolydian/G7.")]
    public static async Task<string> GaArpeggioSuggestions(
        [Description("Chord symbols to analyse, e.g. [\"Am\",\"F\",\"C\",\"G\"]")]
        string[] chords,
        [Description("Optional key, e.g. 'C major' or 'A minor'. Inferred from chords if omitted.")]
        string key = "")
    {
        if (chords is not { Length: > 0 })
            return JsonSerializer.Serialize(new { error = "No chords provided." });

        // Determine key.
        string keyName;
        string modeName;

        if (!string.IsNullOrWhiteSpace(key))
        {
            var parts = key.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            keyName  = parts.Length >= 1 ? parts[0] : "C";
            modeName = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "major";
        }
        else
        {
            // Auto-detect via domain.analyzeProgression.
            var raw = await GuitaristHelpers.InvokeAsync(
                "domain.analyzeProgression",
                ("chords", string.Join(" ", chords)));
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            keyName  = "C";
            modeName = "major";
            if (lines.Length >= 1 && lines[0].StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
            {
                var kp = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (kp.Length >= 3) { keyName = kp[1]; modeName = kp[2].ToLowerInvariant(); }
            }
        }

        var keyPc       = GuitaristHelpers.ChordRootPc(keyName);
        if (keyPc < 0) keyPc = 0;
        var isMinor     = modeName.Equals("minor", StringComparison.OrdinalIgnoreCase);
        var offsets     = isMinor ? GuitaristHelpers.MinorOffsets : GuitaristHelpers.MajorOffsets;
        var pattern     = isMinor ? GuitaristHelpers.MinorPattern : GuitaristHelpers.MajorPattern;
        var romans      = isMinor ? GuitaristHelpers.MinorRomans  : GuitaristHelpers.MajorRomans;
        var degreeModes = isMinor ? MinorDegreeModes : MajorDegreeModes;

        var suggestions = chords.Select(chord =>
        {
            var chordPc = GuitaristHelpers.ChordRootPc(chord);
            if (chordPc < 0)
                return new { chord, scaleDegree = "?", arpeggio = "?", mode = "unknown", notes = "" };

            // Find which scale degree this chord root matches.
            var degIdx = Array.FindIndex(offsets, o => (keyPc + o) % 12 == chordPc);
            if (degIdx < 0)
            {
                // Chromatic chord — use generic major arpeggio suggestion.
                return new
                {
                    chord,
                    scaleDegree = "chromatic",
                    arpeggio    = chord + " (chromatic — outside key)",
                    mode        = "depends on context",
                    notes       = "R, M2, M3, P5"
                };
            }

            var (arpeggioSuffix, modeName2, notes) = degreeModes[degIdx];
            return new
            {
                chord,
                scaleDegree = romans[degIdx],
                arpeggio    = chord + arpeggioSuffix,
                mode        = modeName2,
                notes
            };
        }).ToList();

        var result = new
        {
            key         = $"{keyName} {modeName}",
            suggestions
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}

// ── Tool 4: GaEasierVoicings ───────────────────────────────────────────────────

[McpServerToolType]
public static class GaEasierVoicingsTool
{
    // Standard open-position chord voicings.  Each entry: symbol → (frets per string
    // E-A-D-G-B-e, x = muted, 0 = open).
    // Difficulty is 1-5 (1 = easiest).
    private static readonly (string Symbol, string Frets, int Difficulty, bool HasBarre, string Notes)[] OpenVoicings =
    [
        // Major open shapes
        ("C",    "x32010", 1, false, "C, E, G"),
        ("D",    "xx0232", 1, false, "D, F#, A"),
        ("E",    "022100", 1, false, "E, G#, B"),
        ("G",    "320003", 1, false, "G, B, D"),
        ("A",    "x02220", 1, false, "A, C#, E"),
        ("F",    "133211", 3, true,  "F, A, C"),
        ("Bb",   "x13331", 3, true,  "Bb, D, F"),
        ("B",    "x24442", 3, true,  "B, D#, F#"),

        // Minor open shapes
        ("Am",   "x02210", 1, false, "A, C, E"),
        ("Em",   "022000", 1, false, "E, G, B"),
        ("Dm",   "xx0231", 1, false, "D, F, A"),
        ("Bm",   "x24432", 3, true,  "B, D, F#"),

        // Dominant 7th
        ("E7",   "020100", 1, false, "E, G#, B, D"),
        ("A7",   "x02020", 1, false, "A, C#, E, G"),
        ("D7",   "xx0212", 1, false, "D, F#, A, C"),
        ("G7",   "320001", 2, false, "G, B, D, F"),
        ("C7",   "x32310", 2, false, "C, E, G, Bb"),
        ("B7",   "x21202", 2, false, "B, D#, F#, A"),

        // Major 7th
        ("Cmaj7","x32000", 1, false, "C, E, G, B"),
        ("Gmaj7","320002", 1, false, "G, B, D, F#"),
        ("Amaj7","x02120", 1, false, "A, C#, E, G#"),
        ("Dmaj7","xx0222", 1, false, "D, F#, A, C#"),
        ("Emaj7","021100", 2, false, "E, G#, B, D#"),
        ("Fmaj7","103210", 2, false, "F, A, C, E"),

        // Minor 7th
        ("Am7",  "x02010", 1, false, "A, C, E, G"),
        ("Em7",  "020000", 1, false, "E, G, B, D"),
        ("Dm7",  "xx0211", 1, false, "D, F, A, C"),

        // Suspended
        ("Dsus2","xx0230", 1, false, "D, E, A"),
        ("Asus2","x02200", 1, false, "A, B, E"),
        ("Esus2","024400", 2, false, "E, F#, B"),
        ("Dsus4","xx0233", 1, false, "D, G, A"),
        ("Asus4","x02230", 1, false, "A, D, E"),
        ("Esus4","022200", 1, false, "E, A, B"),
        ("Gsus4","310013", 2, false, "G, C, D"),

        // Power chords
        ("C5",   "x3500x", 1, false, "C, G"),
        ("D5",   "xx023x", 1, false, "D, A"),
        ("E5",   "022xxx", 1, false, "E, B"),
        ("G5",   "3500xx", 1, false, "G, D"),
        ("A5",   "x022xx", 1, false, "A, E"),
        ("B5",   "x244xx", 2, false, "B, F#"),
        ("F5",   "133xxx", 2, true,  "F, C"),
    ];

    // Enharmonic aliases for root matching.
    private static readonly IReadOnlyDictionary<string, string> Enharmonics =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["C#"] = "Db", ["D#"] = "Eb", ["F#"] = "Gb",
            ["G#"] = "Ab", ["A#"] = "Bb", ["Db"] = "C#",
            ["Eb"] = "D#", ["Gb"] = "F#", ["Ab"] = "G#", ["Bb"] = "A#",
        };

    [McpServerTool]
    [Description(
        "Find easier, beginner-friendly alternative voicings for a chord — ideal when barre chords are painful or difficult. " +
        "Supports constraints: 'no-barre' (avoid barre chords), 'open-position' (use open strings), 'max-fret-span:N' (limit finger stretch). " +
        "Example: F with no-barre → Fmaj7 (103210), Fsus2 (133011), or C/F alternatives at a simpler fret span.")]
    public static async Task<string> GaEasierVoicings(
        [Description("Chord symbol to find alternatives for, e.g. 'F', 'Bm', 'Bb'")]
        string chord,
        [Description("Optional playability constraint: 'no-barre', 'open-position', or 'max-fret-span:N' (e.g. 'max-fret-span:3')")]
        string constraint = "no-barre")
    {
        if (string.IsNullOrWhiteSpace(chord))
            return JsonSerializer.Serialize(new { error = "No chord provided." });

        // Parse constraint.
        var noBarre    = constraint.Contains("no-barre",    StringComparison.OrdinalIgnoreCase);
        var openPos    = constraint.Contains("open-position", StringComparison.OrdinalIgnoreCase);
        int maxSpan    = 99;
        if (constraint.StartsWith("max-fret-span:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(constraint["max-fret-span:".Length..], out var span))
        {
            maxSpan = span;
        }

        // Normalise chord symbol for lookup (try both spellings).
        var inputNorm  = chord.Trim();
        var inputRoot  = ExtractRoot(inputNorm);
        var inputSuffix = inputNorm[inputRoot.Length..];

        // Step 1 — use domain.chordSubstitutions to get harmonically similar chords.
        var subsRaw = await GuitaristHelpers.InvokeAsync(
            "domain.chordSubstitutions",
            ("symbol", chord));

        // Step 2 — match alternatives from open-voicing table, plus substitutions.
        var candidates = new List<(string Symbol, string Frets, int Difficulty, bool HasBarre, string Notes, string Source)>();

        // Add direct open voicings that match the root or are diatonic substitutions.
        foreach (var v in OpenVoicings)
        {
            var vRoot = ExtractRoot(v.Symbol);

            // Include if: same root (enharmonic), or same quality suffix class.
            var sameRoot = vRoot.Equals(inputRoot, StringComparison.OrdinalIgnoreCase) ||
                          (Enharmonics.TryGetValue(inputRoot, out var enh) &&
                           vRoot.Equals(enh, StringComparison.OrdinalIgnoreCase));

            if (!sameRoot) continue;
            if (v.Symbol.Equals(inputNorm, StringComparison.OrdinalIgnoreCase)) continue; // skip exact match

            // Compute fret span (ignoring muted / open strings).
            var fretNums = v.Frets
                .Where(c => c is >= '1' and <= '9')
                .Select(c => (int)(c - '0'))
                .ToList();
            var fretSpan = fretNums.Count > 0 ? fretNums.Max() - fretNums.Min() : 0;

            if (noBarre && v.HasBarre) continue;
            if (openPos && !v.Frets.Contains('0')) continue;
            if (fretSpan > maxSpan) continue;

            candidates.Add((v.Symbol, v.Frets, v.Difficulty, v.HasBarre, v.Notes, "open voicing"));
        }

        // Also include same-suffix open voicings from enharmonic spellings.
        if (candidates.Count == 0 && Enharmonics.TryGetValue(inputRoot, out var enhRoot))
        {
            var altChord = enhRoot + inputSuffix;
            foreach (var v in OpenVoicings)
            {
                if (!v.Symbol.Equals(altChord, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add((v.Symbol, v.Frets, v.Difficulty, v.HasBarre, v.Notes, "enharmonic voicing"));
            }
        }

        // Deduplicate and sort by difficulty (easiest first).
        var alternatives = candidates
            .DistinctBy(c => c.Symbol)
            .OrderBy(c => c.Difficulty)
            .Take(5)
            .Select(c => new
            {
                voicing    = $"{c.Symbol} [{c.Frets}]",
                difficulty = c.Difficulty switch
                {
                    1 => "very easy",
                    2 => "easy",
                    3 => "intermediate",
                    4 => "hard",
                    _ => "advanced"
                },
                notes    = c.Notes,
                hasBarre = c.HasBarre,
                source   = c.Source,
            })
            .ToList();

        // Include a snippet from the substitutions text for context.
        var subsSnippet = subsRaw.StartsWith("Error") ? null : subsRaw;

        var result = new
        {
            chord,
            constraint  = string.IsNullOrWhiteSpace(constraint) ? "no-barre" : constraint,
            alternatives,
            substitutionHints = subsSnippet
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Extract the root note prefix from a chord symbol (e.g. "Bb" from "Bbm7").</summary>
    private static string ExtractRoot(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return symbol;
        if (symbol.Length >= 2 && symbol[1] is '#' or 'b')
            return symbol[..2];
        return symbol[..1];
    }
}
