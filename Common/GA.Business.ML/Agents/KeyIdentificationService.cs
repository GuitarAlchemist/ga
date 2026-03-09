namespace GA.Business.ML.Agents;

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Identifies the most likely key(s) for a chord progression by scoring each
/// candidate key using pitch-class arithmetic from the GA domain model.
/// </summary>
/// <remarks>
/// Algorithm: for each of the 30 major/minor keys in <see cref="Key.Items"/>, count how many
/// input chords are fully diatonic (root pitch class in the key AND triad quality matches the
/// expected scale-degree quality). Keys are returned sorted by descending score.
/// Ties (relative key pairs always share the same score) are preserved together.
/// </remarks>
public static partial class KeyIdentificationService
{
    /// <summary>Result for one candidate key.</summary>
    public record KeyCandidate(
        string Key,
        string RelativeKey,
        int MatchCount,
        int TotalChords,
        string[] DiatonicSet);

    // ── Internal chord quality model ──────────────────────────────────────────

    private enum ChordQuality { Major, Minor, Diminished }

    // Natural major: I M, II m, III m, IV M, V M, VI m, VII dim
    private static readonly ChordQuality[] MajorPattern =
    [
        ChordQuality.Major, ChordQuality.Minor, ChordQuality.Minor,
        ChordQuality.Major, ChordQuality.Major, ChordQuality.Minor,
        ChordQuality.Diminished
    ];

    // Natural minor: I m, II dim, III M, IV m, V m, VI M, VII M
    private static readonly ChordQuality[] MinorPattern =
    [
        ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Major,
        ChordQuality.Minor, ChordQuality.Minor, ChordQuality.Major,
        ChordQuality.Major
    ];

    // ── Domain-derived key data (built once at startup from Key.Items) ─────────

    private sealed record DomainKeyData(
        string Name,                                      // "C major" / "A minor"
        string RelativeName,                              // "A minor" / "C major"
        string[] DiatonicSymbols,                         // ["C", "Dm", "Em", "F", "G", "Am", "Bdim"]
        FrozenSet<int> KeyPitchClasses,                   // 7 PCs for fast root containment check
        (int RootPc, ChordQuality Quality)[] DiatonicTriads); // for exact quality matching

    private static readonly IReadOnlyList<DomainKeyData> AllKeys = BuildAllKeys();

    private static List<DomainKeyData> BuildAllKeys()
    {
        static string FormatKeyName(Key key) =>
            $"{key.Root} {(key.KeyMode == KeyMode.Major ? "major" : "minor")}";

        static string ChordSymbol(string noteStr, ChordQuality quality) => quality switch
        {
            ChordQuality.Minor       => $"{noteStr}m",
            ChordQuality.Diminished  => $"{noteStr}dim",
            _                        => noteStr
        };

        // First pass: build data for all 30 keys
        var items = Key.Items.Select(key =>
        {
            var notes    = key.Notes.ToList();
            var pattern  = key.KeyMode == KeyMode.Major ? MajorPattern : MinorPattern;
            var triads   = notes.Select((n, i) => (n.PitchClass.Value, pattern[i])).ToArray();
            var symbols  = notes.Select((n, i) => ChordSymbol(n.ToString(), pattern[i])).ToArray();
            var pcs      = notes.Select(n => n.PitchClass.Value).ToFrozenSet();
            var name     = FormatKeyName(key);
            return (key, name, pcs, triads, symbols);
        }).ToList();

        // Build pitch-class mask → key names for relative-key lookup
        // (Two keys with identical pitch-class sets are relative major/minor)
        var byMask = new Dictionary<int, List<(string Name, KeyMode Mode)>>();
        foreach (var (key, name, pcs, _, _) in items)
        {
            var mask = pcs.Aggregate(0, (acc, pc) => acc | (1 << pc));
            if (!byMask.TryGetValue(mask, out var list))
                byMask[mask] = list = [];
            list.Add((name, key.KeyMode));
        }

        // Second pass: fill relative names using the mask map
        return [.. items.Select(item =>
        {
            var (key, name, pcs, triads, symbols) = item;
            var mask     = pcs.Aggregate(0, (acc, pc) => acc | (1 << pc));
            var sibling  = byMask.GetValueOrDefault(mask)
                               ?.FirstOrDefault(x => x.Mode != key.KeyMode);
            return new DomainKeyData(name, sibling?.Name ?? string.Empty, symbols, pcs, triads);
        })];
    }

    // ── Chord root → pitch class (all enharmonic spellings) ──────────────────

    private static readonly Dictionary<string, int> RootPcMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0,   ["B#"] = 0,
        ["C#"] = 1,  ["Db"] = 1,
        ["D"] = 2,
        ["D#"] = 3,  ["Eb"] = 3,
        ["E"] = 4,   ["Fb"] = 4,
        ["F"] = 5,   ["E#"] = 5,
        ["F#"] = 6,  ["Gb"] = 6,
        ["G"] = 7,
        ["G#"] = 8,  ["Ab"] = 8,
        ["A"] = 9,
        ["A#"] = 10, ["Bb"] = 10,
        ["B"] = 11,  ["Cb"] = 11
    };

    /// <summary>
    /// Parses a chord symbol (after extension stripping) into its root pitch class and triad quality.
    /// Returns <c>null</c> for unrecognised chord symbols.
    /// </summary>
    private static (int RootPc, ChordQuality Quality)? ParseChordRootAndQuality(string chord)
    {
        var s = NormalizeChord(chord);
        if (string.IsNullOrEmpty(s)) return null;

        // Extract root: 1 letter + optional accidental (#/b)
        var rootStr = s.Length >= 2 && s[1] is '#' or 'b' ? s[..2] : s[..1];
        if (!RootPcMap.TryGetValue(rootStr, out var rootPc)) return null;

        var quality = s[rootStr.Length..].ToLowerInvariant() switch
        {
            "m"   => ChordQuality.Minor,
            "dim" => ChordQuality.Diminished,
            _     => ChordQuality.Major   // covers empty, "maj", "aug", etc.
        };

        return (rootPc, quality);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scores all 30 major/minor keys using pitch-class arithmetic and returns candidates
    /// with at least one diatonic match, sorted by descending match count then by key name.
    /// </summary>
    public static IReadOnlyList<KeyCandidate> Identify(IEnumerable<string> chordSymbols)
    {
        var parsed = chordSymbols
            .Select(ParseChordRootAndQuality)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .Distinct()
            .ToList();

        if (parsed.Count == 0)
            return [];

        return [.. AllKeys
            .Select(kd =>
            {
                var matchCount = parsed.Count(chord =>
                    kd.DiatonicTriads.Any(t => t.RootPc == chord.RootPc && t.Quality == chord.Quality));

                return new KeyCandidate(
                    Key: kd.Name,
                    RelativeKey: kd.RelativeName,
                    MatchCount: matchCount,
                    TotalChords: parsed.Count,
                    DiatonicSet: kd.DiatonicSymbols);
            })
            .Where(c => c.MatchCount > 0)
            .OrderByDescending(c => c.MatchCount)
            .ThenBy(c => c.Key)];
    }

    /// <summary>
    /// Extracts chord symbols from free-form text.
    /// Handles "Am F C G", "Am, F, C, G", "I play Am then F..." etc.
    /// </summary>
    public static IReadOnlyList<string> ExtractChords(string query)
    {
        var matches = ChordPattern().Matches(query);
        return [.. matches.Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Detects whether a query is asking to identify the key.</summary>
    public static bool IsKeyIdentificationQuery(string query)
    {
        var q = query.ToLowerInvariant();
        return (q.Contains("what key") || q.Contains("which key") ||
                q.Contains("what scale") || q.Contains("identify the key") ||
                q.Contains("key am i") || q.Contains("key is this") ||
                q.Contains("key do these") || q.Contains("key are these") ||
                q.Contains("find the key") || q.Contains("determine the key"))
               && ExtractChords(query).Count >= 2;
    }

    // Strips extensions (7, maj7, sus4, add9…) and normalises enharmonics
    // e.g. "G7" → "G", "Cmaj7" → "C", "Am7" → "Am", "Bdim7" → "Bdim", "A#m" → "Bbm"
    private static string NormalizeChord(string chord)
    {
        var s = Regex.Replace(chord.Trim(), @"(maj|min|aug|sus|add)?\d+.*$", "", RegexOptions.IgnoreCase);
        return s.Replace("A#", "Bb").Replace("D#", "Eb").Replace("G#", "Ab");
    }

    [GeneratedRegex(@"\b[A-G][b#]?(m|dim|aug|maj)?\b", RegexOptions.None)]
    private static partial Regex ChordPattern();
}
