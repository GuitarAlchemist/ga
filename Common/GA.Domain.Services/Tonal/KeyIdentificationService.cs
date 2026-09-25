namespace GA.Domain.Services.Tonal;

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

    private enum ChordQuality { Major, Minor, Diminished, Dominant }

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

        var normalizedQuality = s[rootStr.Length..].ToLowerInvariant();
        var isDominant = chord.Contains('7') &&
                         string.IsNullOrEmpty(normalizedQuality) &&
                         !chord.Contains("maj", StringComparison.OrdinalIgnoreCase);

        if (isDominant)
        {
            return (rootPc, ChordQuality.Dominant);
        }

        var quality = normalizedQuality switch
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
    /// with at least one diatonic match, sorted by descending match count plus cadence weight.
    /// Ties go to the key whose tonic triad opens the progression, then to the major key, then
    /// to the key name.
    /// </summary>
    /// <remarks>
    ///     Pitch content alone cannot separate a key from its relative when a secondary dominant is
    ///     present: in <c>C E7 Am F G7 C</c>, E7 (V7/vi) fits A harmonic minor but not C major, so A
    ///     minor matches more chords. The ending decides it: a progression that closes on the key's
    ///     dominant then tonic (an authentic cadence) adds two. A progression that merely stops on a
    ///     chord adds nothing, so relative keys stay tied and a half cadence does not promote the key
    ///     of its last chord.
    ///     A tie is common: a key and its relative share every diatonic triad. It goes to the key
    ///     whose tonic triad is the first chord (<c>Am F C G</c> is A minor, <c>C G Am F</c> C major),
    ///     then to the major key. Callers take this order as it is, so every tool names the same key.
    ///     <see cref="KeyCandidate.MatchCount" /> stays the plain diatonic count.
    /// </remarks>
    public static IReadOnlyList<KeyCandidate> Identify(IEnumerable<string> chordSymbols)
    {
        var ordered = chordSymbols
            .Select(ParseChordRootAndQuality)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();
        var parsed = ordered.Distinct().ToList();

        if (parsed.Count == 0)
            return [];

        return [.. AllKeys
            .Select(kd =>
            {
                var matchCount = parsed.Count(chord =>
                    kd.DiatonicTriads.Any(t =>
                    {
                        if (t.RootPc != chord.RootPc) return false;
                        if (chord.Quality == ChordQuality.Dominant)
                        {
                            var isMajorKey = kd.Name.Contains("major", StringComparison.OrdinalIgnoreCase);
                            var idx = Array.FindIndex(kd.DiatonicTriads, x => x.RootPc == t.RootPc);
                            if (isMajorKey)
                            {
                                return idx == 4 && t.Quality == ChordQuality.Major;
                            }
                            else
                            {
                                // VII7 only: its four notes are all in the natural minor scale. The harmonic-minor
                                // V7 is not, so it is recognised by CadenceWeight when it resolves to i,
                                // instead of making every V7/vi in a major key pull toward the relative minor.
                                return idx == 6 && t.Quality == ChordQuality.Major;
                            }
                        }
                        return t.Quality == chord.Quality;
                    }));

                return (Candidate: new KeyCandidate(
                    Key: kd.Name,
                    RelativeKey: kd.RelativeName,
                    MatchCount: matchCount,
                    TotalChords: parsed.Count,
                    DiatonicSet: kd.DiatonicSymbols),
                    Cadence: CadenceWeight(kd, ordered),
                    OpensOnTonic: ordered[0].RootPc == kd.DiatonicTriads[0].RootPc
                                  && ordered[0].Quality == kd.DiatonicTriads[0].Quality);
            })
            .Where(s => s.Candidate.MatchCount > 0)
            .OrderByDescending(s => s.Candidate.MatchCount + s.Cadence)
            .ThenByDescending(s => s.OpensOnTonic)
            .ThenByDescending(s => s.Candidate.Key.EndsWith("major", StringComparison.OrdinalIgnoreCase))
            .ThenBy(s => s.Candidate.Key)
            .Select(s => s.Candidate)];
    }

    // 2 when the progression ends on the key's dominant (major triad or dominant seventh on
    // degree 5, so V7 of harmonic minor counts) followed by its tonic triad; otherwise 0.
    private static int CadenceWeight(DomainKeyData kd, IReadOnlyList<(int RootPc, ChordQuality Quality)> ordered)
    {
        if (ordered.Count == 0) return 0;

        var tonic = kd.DiatonicTriads[0];
        var last = ordered[^1];
        if (ordered.Count < 2 || last.RootPc != tonic.RootPc || last.Quality != tonic.Quality) return 0;

        var before = ordered[^2];
        var dominantRoot = kd.DiatonicTriads[4].RootPc;
        var isDominant = before.RootPc == dominantRoot
                         && before.Quality is ChordQuality.Major or ChordQuality.Dominant;
        return isDominant ? 2 : 0;
    }

    /// <summary>
    /// Checks if a chord symbol is diatonic to a key.
    /// </summary>
    public static bool IsChordDiatonic(string keyName, string chordSymbol)
    {
        var keyData = AllKeys.FirstOrDefault(k => k.Name.Equals(keyName, StringComparison.OrdinalIgnoreCase));
        if (keyData == null) return false;

        var parsed = ParseChordRootAndQuality(chordSymbol);
        if (!parsed.HasValue) return false;

        var (rootPc, quality) = parsed.Value;

        // In a minor key the dominant is conventionally major (harmonic minor): E and E7 belong to
        // A minor when a progression is labelled. Identify does not count them, so that a V7/vi in
        // a major key does not pull toward the relative minor; see CadenceWeight.
        var isMinorKey = keyData.Name.Contains("minor", StringComparison.OrdinalIgnoreCase);
        if (isMinorKey && rootPc == keyData.DiatonicTriads[4].RootPc
                       && quality is ChordQuality.Major or ChordQuality.Dominant)
            return true;

        return keyData.DiatonicTriads.Any(t =>
        {
            if (t.RootPc != rootPc) return false;
            if (quality == ChordQuality.Dominant)
            {
                var isMajorKey = keyData.Name.Contains("major", StringComparison.OrdinalIgnoreCase);
                var idx = Array.FindIndex(keyData.DiatonicTriads, x => x.RootPc == t.RootPc);
                if (isMajorKey)
                {
                    return idx == 4 && t.Quality == ChordQuality.Major;
                }
                else
                {
                    // VII7 only: its four notes are all in the natural minor scale. The harmonic-minor
                    // V7 is not, so it is recognised by CadenceWeight when it resolves to i,
                    // instead of making every V7/vi in a major key pull toward the relative minor.
                    return idx == 6 && t.Quality == ChordQuality.Major;
                }
            }
            return t.Quality == quality;
        });
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
        var s = Regex.Replace(chord.Trim(), "min", "m", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"(maj|aug|sus|add)?\d+.*$", "", RegexOptions.IgnoreCase);
        return s.Replace("A#", "Bb").Replace("D#", "Eb").Replace("G#", "Ab");
    }

    // The root is case-sensitive: with IgnoreCase, prose such as "I am composing" yielded the chord
    // "am", so a message with no chords got a key analysis instead of a decline.
    [GeneratedRegex(@"\b[A-G][b#]?(?:maj|Maj|min|m|dim|aug|sus|add)?\d*(?:b5|#5|b9|#9|#11|b13)?\b", RegexOptions.None)]
    private static partial Regex ChordPattern();
}
