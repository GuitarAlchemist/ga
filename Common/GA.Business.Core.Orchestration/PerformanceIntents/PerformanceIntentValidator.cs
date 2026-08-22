namespace GA.Business.Core.Orchestration.PerformanceIntents;

using GA.Domain.Core.Theory.Harmony;

/// <summary>
/// Validates a <see cref="PerformanceIntent"/> against the deterministic theory engine.
/// The LLM proposes the intent; this class decides whether it is true. Two checks target
/// the #567 bug class directly:
/// <list type="number">
///   <item><b>Well-formed symbols.</b> Every <c>chord</c> and <c>arpeggio</c> must parse via
///     the strict <see cref="Chord.TryFromSymbol"/> (which throws on unrecognised suffixes) —
///     this rejects the concatenation artefact <c>Amm7</c> that the old arpeggio tool emitted.</item>
///   <item><b>Key-aware degrees.</b> A chord mapped to a diatonic arpeggio must actually be
///     diatonic to the key: all of its pitch classes must belong to the key's scale. A borrowed
///     or secondary chord (e.g. an A major chord in C major, whose C# is not in the scale) is
///     flagged rather than silently forced onto the wrong mode.</item>
/// </list>
/// Invalid intents are refused deterministically — never patched into a plausible-looking answer.
/// </summary>
public sealed class PerformanceIntentValidator
{
    // Diatonic semitone offsets from the key root (Ionian / Aeolian). Same vocabulary the
    // arpeggio MCP tool and ImprovisationSkill use; kept local so the validator has no
    // dependency on file-scoped helpers elsewhere.
    private static readonly int[] MajorSteps = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorSteps = [0, 2, 3, 5, 7, 8, 10];

    /// <summary>
    /// Validate <paramref name="intent"/>. Returns <see cref="IntentValidation.Valid"/> when every
    /// suggestion is well-formed and key-consistent, otherwise a failure carrying human-readable
    /// problems (one per defect) for a deterministic "cannot answer" response.
    /// </summary>
    public IntentValidation Validate(PerformanceIntent? intent)
    {
        if (intent is null)
            return IntentValidation.Fail("The model produced no parseable performance intent.");

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(intent.Key))
            problems.Add("Intent is missing a key.");

        if (intent.SuggestedArpeggios is not { Count: > 0 })
            problems.Add("Intent contains no arpeggio suggestions.");

        // Resolve the key's diatonic pitch classes once. If the key itself is unparseable there
        // is no ground truth to validate against, so we stop here.
        var diatonic = TryResolveKey(intent.Key, out var keyLabel);
        if (diatonic is null)
        {
            if (!string.IsNullOrWhiteSpace(intent.Key))
                problems.Add($"Unrecognized key '{intent.Key}'.");
            return IntentValidation.Fail(problems);
        }

        foreach (var s in intent.SuggestedArpeggios ?? [])
        {
            var chordSym = s.Chord?.Trim() ?? "";
            var arpSym   = s.Arpeggio?.Trim() ?? "";

            if (!Chord.TryFromSymbol(chordSym, out var chord) || chord is null)
            {
                problems.Add($"'{chordSym}' is not a valid chord symbol.");
                continue;
            }

            if (!Chord.TryFromSymbol(arpSym, out var arp) || arp is null)
            {
                // The #567 'Amm7' case lands here: the strict parser rejects the 'mm7' suffix.
                problems.Add($"Suggested arpeggio '{arpSym}' for {chordSym} is not a valid chord symbol.");
                continue;
            }

            // An arpeggio played over a chord is rooted on that chord.
            if (arp.Root.PitchClass.Value != chord.Root.PitchClass.Value)
                problems.Add($"Arpeggio '{arpSym}' is not rooted on {chordSym}.");

            // Key-aware degree check: a diatonic-mode suggestion is only valid when the chord is
            // actually in the key. Borrowed / secondary chords must not be mapped to a diatonic mode.
            if (chord.PitchClassSet.Any(pc => !diatonic.Contains(pc.Value)))
                problems.Add(
                    $"{chordSym} is not diatonic to {keyLabel} (borrowed/secondary chord); " +
                    "it cannot be mapped to a diatonic arpeggio in v1.");
        }

        return problems.Count == 0 ? IntentValidation.Valid : IntentValidation.Fail(problems);
    }

    /// <summary>
    /// Parse a key string like <c>"C major"</c> / <c>"A minor"</c> into its diatonic pitch-class set.
    /// Returns <see langword="null"/> when the root or mode cannot be resolved.
    /// </summary>
    private static HashSet<int>? TryResolveKey(string? key, out string label)
    {
        label = key?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key)) return null;

        var parts = key.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var root = parts[0];
        var mode = parts[1].ToLowerInvariant();
        var isMinor = mode is "minor" or "min" or "aeolian";
        if (!isMinor && mode is not ("major" or "maj" or "ionian")) return null;

        // Reuse the theory engine to turn the key root into a pitch class.
        if (!Chord.TryFromSymbol(root, out var rootChord) || rootChord is null) return null;
        var rootPc = rootChord.Root.PitchClass.Value;

        var steps = isMinor ? MinorSteps : MajorSteps;
        label = $"{root} {(isMinor ? "minor" : "major")}";
        return steps.Select(step => (rootPc + step) % 12).ToHashSet();
    }
}

/// <summary>Outcome of <see cref="PerformanceIntentValidator.Validate"/>.</summary>
public sealed record IntentValidation(bool IsValid, IReadOnlyList<string> Problems)
{
    public static readonly IntentValidation Valid = new(true, []);

    public static IntentValidation Fail(string problem) => new(false, [problem]);
    public static IntentValidation Fail(IReadOnlyList<string> problems) => new(false, problems);
}
