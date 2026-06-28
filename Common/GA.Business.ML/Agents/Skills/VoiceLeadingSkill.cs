namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Domain-backed voice-leading skill. Given two chords (e.g. <b>C → F</b>),
/// finds the optimal pitch-class assignment — pairs each note in chord A with
/// a note in chord B that minimizes total semitone movement, mod 12, picking
/// the shorter direction per voice (up or down ≤6 semitones).
/// Surface forms:
/// <list type="bullet">
/// <item><b>"Voice leading from C to F"</b></item>
/// <item><b>"Smooth voice leading C to Am"</b></item>
/// <item><b>"Best voicing from G7 to Cmaj7"</b></item>
/// <item><b>"How do I voice lead Dm7 to G7"</b></item>
/// </list>
/// Zero LLM calls — exhaustive search over permutations (chord size ≤ 5 so
/// 5! = 120 permutations max). Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14 to close BACKLOG dealbreaker #4. Tier-1 deterministic per
/// <c>docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md</c>.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("VoiceLeadingSkill", "progression")]
public sealed class VoiceLeadingSkill(ILogger<VoiceLeadingSkill> logger) : IOrchestratorSkill
{
    public string Name => "VoiceLeading";
    public string Description =>
        "Computes smooth voice leading between two chords by finding the " +
        "pitch-class assignment that minimizes total semitone movement. " +
        "Returns per-voice motion (up/down N semitones) and total cost. " +
        "Pure pitch-class arithmetic — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "voice leading from C to F",
        "smooth voice leading C to Am",
        "best voicing from G7 to Cmaj7",
        "how do I voice lead Dm7 to G7",
        "voice leading C major to G major",
        "smoothest voicing from Em to A7",
        "voice leading Fmaj7 to Bm7b5",
        "what's the smoothest voicing from D to A",
        "voice lead C7 to F",
        "best way to move from G7 to C",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Two-chord pattern: <chord A> to/→/-> <chord B>. The chord token allows
    // root + optional accidental + optional quality keyword + optional digit
    // + optional flat/sharp-with-digit modifiers (b5, #9, b9...) + optional
    // ° symbol for diminished.
    private static readonly Regex TwoChordPattern =
        new(@"\b(?<a>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\s+(?:to|→|->|>)\s+(?<b>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, int> RootPc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"]  = 0,  ["C#"] = 1, ["Db"] = 1,
        ["D"]  = 2,  ["D#"] = 3, ["Eb"] = 3,
        ["E"]  = 4,
        ["F"]  = 5,  ["F#"] = 6, ["Gb"] = 6,
        ["G"]  = 7,  ["G#"] = 8, ["Ab"] = 8,
        ["A"]  = 9,  ["A#"] = 10, ["Bb"] = 10,
        ["B"]  = 11,
    };

    private static readonly string[] SharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly string[] FlatNames =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;
        if (TwoChordPattern.Match(msg) is not { Success: true } match)
            return Task.FromResult(CannotHandle());

        var aToken = match.Groups["a"].Value;
        var bToken = match.Groups["b"].Value;

        var preferFlats = TokenHasFlats(aToken) || TokenHasFlats(bToken)
                          || msg.Contains("flat", StringComparison.OrdinalIgnoreCase);

        if (TryParseChord(aToken) is not { } chordA)
            return Task.FromResult(CannotParse(aToken));
        if (TryParseChord(bToken) is not { } chordB)
            return Task.FromResult(CannotParse(bToken));

        return Task.FromResult(AnswerVoiceLeading(aToken, chordA, bToken, chordB, preferFlats));
    }

    private AgentResponse AnswerVoiceLeading(
        string aLabel, int[] chordA,
        string bLabel, int[] chordB,
        bool preferFlats)
    {
        // For an exhaustive minimum-cost matching, we permute the SHORTER chord
        // against subsets of the larger. To keep the explanation crisp, we pad
        // the smaller chord by repeating its root so |A| = |B| for assignment
        // — this is the standard voice-leading framing when voice counts
        // differ (e.g. triad → 7th chord adds a voice that gets the new tone).
        var n = Math.Max(chordA.Length, chordB.Length);
        var paddedA = Pad(chordA, n);
        var paddedB = Pad(chordB, n);

        var (bestPerm, bestCost) = FindBestAssignment(paddedA, paddedB);

        var sb = new StringBuilder();
        sb.AppendLine($"**Smoothest voice leading {aLabel} → {bLabel}**: total motion = **{bestCost} semitones**.");
        sb.AppendLine();
        sb.AppendLine("| Voice | From | To | Move |");
        sb.AppendLine("|-------|------|----|----|");
        for (var i = 0; i < n; i++)
        {
            var from = paddedA[i];
            var to = paddedB[bestPerm[i]];
            var move = ShortestSignedSemitones(from, to);
            var direction = move switch
            {
                0      => "stays",
                > 0    => $"+{move} (up)",
                _      => $"{move} (down)",
            };
            sb.AppendLine($"| {i + 1} | {Spell(from, preferFlats)} | {Spell(to, preferFlats)} | {direction} |");
        }
        sb.AppendLine();
        sb.AppendLine($"This is the optimal pitch-class assignment — every other voicing of {aLabel} → {bLabel} requires more total semitone movement.");

        return Result(sb.ToString(), $"voice-leading({aLabel}→{bLabel}, cost={bestCost})");
    }

    /// <summary>
    /// Brute-force permutation search. n ≤ 5 so n! ≤ 120 — fine.
    /// </summary>
    private static (int[] bestPerm, int bestCost) FindBestAssignment(int[] a, int[] b)
    {
        var n = a.Length;
        var perm = new int[n];
        for (var i = 0; i < n; i++) perm[i] = i;

        var bestCost = int.MaxValue;
        var bestPerm = (int[])perm.Clone();

        do
        {
            var cost = 0;
            for (var i = 0; i < n; i++)
                cost += Math.Abs(ShortestSignedSemitones(a[i], b[perm[i]]));
            if (cost < bestCost)
            {
                bestCost = cost;
                bestPerm = (int[])perm.Clone();
            }
        } while (NextPermutation(perm));

        return (bestPerm, bestCost);
    }

    private static bool NextPermutation(int[] arr)
    {
        var i = arr.Length - 2;
        while (i >= 0 && arr[i] >= arr[i + 1]) i--;
        if (i < 0) return false;
        var j = arr.Length - 1;
        while (arr[j] <= arr[i]) j--;
        (arr[i], arr[j]) = (arr[j], arr[i]);
        Array.Reverse(arr, i + 1, arr.Length - i - 1);
        return true;
    }

    /// <summary>
    /// Returns the shortest signed semitone distance from <paramref name="from"/> to
    /// <paramref name="to"/>, in the range [-6, 6]. Picks the shorter wrap direction.
    /// </summary>
    private static int ShortestSignedSemitones(int from, int to)
    {
        var diff = ((to - from) % 12 + 12) % 12;  // 0..11
        return diff <= 6 ? diff : diff - 12;
    }

    private static int[] Pad(int[] chord, int targetLen)
    {
        if (chord.Length >= targetLen) return chord;
        var padded = new int[targetLen];
        Array.Copy(chord, padded, chord.Length);
        // Pad with root repeats — the optimizer will assign these to any
        // leftover target voices, and a "stays-on-root" doubling is harmless.
        for (var i = chord.Length; i < targetLen; i++) padded[i] = chord[0];
        return padded;
    }

    /// <summary>
    /// Parse a chord token like "C", "Am", "G7", "Cmaj7", "Dm7", "B°", "F#m7b5"
    /// into pitch classes. Returns null if unparseable.
    /// </summary>
    private static int[]? TryParseChord(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var t = token.Replace("♯", "#").Replace("♭", "b").Trim();

        // Extract root: 1-2 letters at start
        var rootLen = 1;
        if (t.Length >= 2 && (t[1] == '#' || t[1] == 'b')) rootLen = 2;
        if (rootLen > t.Length) return null;
        var rootStr = char.ToUpperInvariant(t[0]) + (rootLen == 2 ? t[1].ToString().ToLowerInvariant() : "");
        if (!RootPc.TryGetValue(rootStr, out var root)) return null;

        var quality = t.Length > rootLen ? t[rootLen..] : string.Empty;
        return BuildChord(root, quality);
    }

    /// <summary>
    /// Build the pitch-class set for a chord given a root PC and a quality
    /// suffix string. Returns <c>null</c> for unknown qualities so callers
    /// surface a parse error instead of silently producing wrong PCs — the
    /// 2026-05-14 correctness review flagged that the old fall-through-to-
    /// major-triad behaviour silently mis-voice-led <c>'C13'</c> /
    /// <c>'7sus4'</c> / <c>'add11'</c> / <c>'m11'</c> as triads.
    /// </summary>
    private static int[]? BuildChord(int root, string quality)
    {
        var q = quality.ToLowerInvariant().Trim();

        // Tone choices: 1, b3, 3, 4, b5, 5, #5, 6, b7, 7, b9, 9, #9, 11, #11, 13
        int[]? intervals = q switch
        {
            "" or "maj" or "major"           => [0, 4, 7],                     // major triad
            "m" or "min" or "minor" or "-"   => [0, 3, 7],                     // minor triad
            "dim" or "°" or "o"              => [0, 3, 6],                     // dim triad
            "aug" or "+"                     => [0, 4, 8],                     // aug triad
            "7"                              => [0, 4, 7, 10],                 // dom 7
            "m7" or "min7" or "-7"           => [0, 3, 7, 10],                 // min 7
            "maj7" or "major7" or "M7" or "Δ7" or "Δ"
                                             => [0, 4, 7, 11],                 // maj 7
            "m7b5" or "ø" or "ø7" or "half-dim" or "min7b5"
                                             => [0, 3, 6, 10],                 // half-dim
            "dim7" or "°7" or "o7"           => [0, 3, 6, 9],                  // dim 7
            "sus2"                           => [0, 2, 7],
            "sus4" or "sus"                  => [0, 5, 7],
            "6"                              => [0, 4, 7, 9],                  // maj 6
            "m6" or "min6"                   => [0, 3, 7, 9],                  // min 6
            "9"                              => [0, 4, 7, 10, 2],              // dom 9
            "maj9"                           => [0, 4, 7, 11, 2],
            "m9" or "min9"                   => [0, 3, 7, 10, 2],
            // Unknown quality — return null so the caller emits CannotParse
            // instead of pretending the chord is a major triad.
            _                                => null,
        };

        if (intervals is null) return null;

        var pcs = new int[intervals.Length];
        for (var i = 0; i < intervals.Length; i++) pcs[i] = (root + intervals[i]) % 12;
        return pcs;
    }

    private static bool TokenHasFlats(string token) =>
        token.IndexOf('b', StringComparison.OrdinalIgnoreCase) > 0  // 'b' at position 0 is the note B, not a flat
        || token.IndexOf('♭') >= 0;

    private static string Spell(int pc, bool preferFlats) =>
        preferFlats ? FlatNames[((pc % 12) + 12) % 12] : SharpNames[((pc % 12) + 12) % 12];

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("VoiceLeadingSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: VoiceLeadingSkill (exhaustive permutation, optimal cost)", evidence],
        };
    }

    private static AgentResponse CannotParse(string chord) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse '{chord}' as a chord. Try a chord-symbol like C, Am, G7, Cmaj7, Dm7, F#m7b5, or B°.",
        Confidence = 0.3f,
        Evidence   = [$"VoiceLeadingSkill: unparseable chord token '{chord}'"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about voice leading between two chords, e.g. \"voice leading from C to F\" or \"smoothest voicing G7 to Cmaj7\".",
        Confidence = 0.1f,
        Evidence   = ["VoiceLeadingSkill: no two-chord pattern in query"],
    };
}
