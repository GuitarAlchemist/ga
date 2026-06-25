namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     The single authority for a voiced chord's <b>mood / style / genre / technique</b> tags.
///     Replaces three taggers that disagreed on the same chord — <see cref="VoicingTagEnricher" />
///     and <c>VoicingHarmonicAnalyzer.GenerateSemanticTags</c> at runtime, and
///     <c>InterpretationService.GenerateSemanticTags</c> at corpus-index time (slice 2). One rule
///     per tag, register-aware, deliberately conservative (≤2 mood tags). Every tag it emits is a
///     canonical name known to <c>SymbolicTagRegistry</c>, so a tag always fires its SYMBOLIC bit —
///     the gap the 2026-04-18 diagnostic surfaced.
/// </summary>
/// <remarks>
///     <para>
///         <b>Not its job:</b> structural / physical tags (<c>register:*</c> aside, which mood rules
///         depend on) such as <c>wide-voicing</c> / <c>close-voicing</c> / <c>consonant</c> /
///         <c>shell-voicing</c> stay with their existing owners (<c>VoicingPhysicalAnalyzer</c>,
///         <c>VoicingHarmonicAnalyzer</c>). The engine owns emotional / genre classification only.
///     </para>
///     <para>Campaign-2 slice C2-#2 — see <c>docs/plans/2026-06-23-arch-deepening-campaign-2-plan.md</c>.</para>
/// </remarks>
public static class ChordClassificationEngine
{
    /// <summary>
    ///     Classifies a voiced chord into canonical mood / style / genre / technique tags.
    ///     Caller is responsible for de-duplicating and merging into its tag bag.
    /// </summary>
    public static IReadOnlyList<string> Classify(ChordClassificationContext ctx)
    {
        if (ctx is null) return [];

        var tags = new List<string>();

        // Register band — only when the voicing has notes to average.
        if (ctx.MeanMidi is { } mean)
        {
            tags.Add(ClassifyRegister(mean));
        }

        tags.AddRange(ClassifyMood(ctx));
        tags.AddRange(ClassifyStyle(ctx));
        tags.AddRange(ClassifyTechnique(ctx));

        return tags;
    }

    /// <summary>
    ///     MIDI-mean-based register classification. Band edges are tuned so guitar open position
    ///     (~E3–E4, mean ~55) reads as "mid-low", bebop comping region (~G3–G4, mean ~62) reads as
    ///     "mid", and chord-melody top strings (~E4–E5, mean ~70+) reads as "mid-high" or "high".
    /// </summary>
    private static string ClassifyRegister(double mean) =>
        mean switch
        {
            < 50 => "register:low",
            < 58 => "register:mid-low",
            < 67 => "register:mid",
            < 76 => "register:mid-high",
            _ => "register:high"
        };

    /// <summary>
    ///     Consonance / brightness-driven mood. Deliberately conservative: emits at most two mood
    ///     tags per voicing to avoid skewing the SYMBOLIC partition toward a single voicing's tag
    ///     density.
    /// </summary>
    private static IEnumerable<string> ClassifyMood(ChordClassificationContext ctx)
    {
        var q = (ctx.Quality ?? "").ToLowerInvariant();
        var isMinor = q.Contains("min") || q.StartsWith("m", StringComparison.Ordinal) && !q.StartsWith("maj", StringComparison.Ordinal);
        var isMajor = q.Contains("maj") || (!isMinor && !q.Contains("dim") && !q.Contains("aug"));
        var isExtended = ctx.HasSeventhOrBeyond;
        var hasDrop = ctx.DropVoicing is not null;
        var midiMean = ctx.MeanMidi ?? 0;

        // "tense" — high dissonance overrides everything.
        if (ctx.DissonanceScore > 0.65)
        {
            yield return "tense";
            yield break;
        }

        // "dreamy" — extended + open/drop voicings in mid-to-high register.
        if (isExtended && (hasDrop || ctx.IsOpenVoicing) && midiMean >= 58)
        {
            yield return "dreamy";
        }

        // "bright" — major + mid-high to high register + consonant.
        if (isMajor && midiMean >= 67 && ctx.Consonance >= 0.60)
        {
            yield return "bright";
        }

        // "melancholy" / "sad" — minor + low-to-mid register.
        if (isMinor && midiMean < 60)
        {
            yield return "melancholy";
            yield return "sad";
        }

        // "stable" — high consonance, triadic, closed voicing.
        if (ctx.Consonance >= 0.75 && ctx.NoteCount <= 4 && !ctx.IsOpenVoicing && !hasDrop)
        {
            yield return "stable";
        }

        // "resonant" — wide spread + open voicing (lots of ringing overtones).
        if (ctx.IsOpenVoicing && ctx.IntervalSpread >= 14)
        {
            yield return "resonant";
        }
    }

    /// <summary>
    ///     Style tags from chord-family heuristics. Under-tagging is preferred to over-tagging: a
    ///     voicing tagged "jazz" should at least be plausibly jazz. Any extended-quality chord
    ///     (7/9/11/13/maj7/m7/m7b5) gets "jazz"; plain triads do not. Drop-2/drop-3/rootless
    ///     structures carry the finer style signal via their own canonical tags.
    /// </summary>
    private static IEnumerable<string> ClassifyStyle(ChordClassificationContext ctx)
    {
        var q = (ctx.Quality ?? "").ToLowerInvariant();
        var isExtended = ctx.HasSeventhOrBeyond;

        // Jazz: any extended quality qualifies. Voicing-pattern tags (drop-*, shell-*, rootless)
        // supply the finer style differentiation.
        if (isExtended)
        {
            yield return "jazz";
        }

        // Neo-soul: rootless extended chords (mid/mid-high register approximated by the proxies).
        if (ctx.IsRootless && isExtended)
        {
            yield return "neo-soul";
        }

        // Rock guitar: power-chord shape (5, or just two notes at P5 interval).
        if (q is "5" || q.StartsWith("5 ") || q.EndsWith(" 5"))
        {
            yield return "rock-guitar";
            yield return "power-chord";
        }

        // Campfire / cowboy chord: simple triad, open voicing, basic quality.
        var isSimpleTriad = ctx.NoteCount <= 5 && (q == "maj" || q == "" || q == "m" || q == "min");
        if (isSimpleTriad && ctx.IsOpenVoicing)
        {
            yield return "campfire-chord";
        }
    }

    /// <summary>Technique tags derived from structural flags.</summary>
    private static IEnumerable<string> ClassifyTechnique(ChordClassificationContext ctx)
    {
        if (ctx.IsRootless) yield return "rootless";

        // closed / open voicing — canonical bit in the vocabulary.
        yield return ctx.IsOpenVoicing ? "open-voicing" : "closed-voicing";

        // Drop-voicing canonical form. Stored e.g. "Drop-2"; canonical key is "drop-2-voicings".
        if (ctx.DropVoicing is { } d)
        {
            var norm = d.ToLowerInvariant().Replace(" ", "-");
            if (norm.Contains("drop-2")) yield return "drop-2-voicings";
            else if (norm.Contains("drop-3")) yield return "drop-3-voicings";
        }

        // Shell voicing heuristic: rootless + exactly 3 notes covers the canonical jazz shell.
        if (ctx.IsRootless && ctx.NoteCount == 3)
        {
            yield return "shell-voicing";
        }
    }
}
