namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using GA.Business.Core.Analysis.Voicings;

/// <summary>
///     Derives mood / register / style / technique tags from metrics the voicing analyzer
///     already computes. Output tags use the canonical names known to
///     <c>SymbolicTagRegistry</c> so the SYMBOLIC embedding partition fires.
///
///     <para>
///         The 2026-04-18 diagnostic run revealed that style-tag queries like
///         <c>Cmaj7 jazz</c> scored <i>lower</i> than bare chord queries because top-ranked
///         corpus voicings had thin SYMBOLIC bits — enrichment was never populating the
///         style/mood dimensions. This class closes that gap without expanding the
///         partition schema or requiring new analysis work.
///     </para>
///
///     <para>
///         Contract: every tag returned is a canonical name from the registry at the time
///         of writing. Non-matching tags are silently dropped by the downstream
///         <c>SymbolicVectorService</c>, so adding a guess-wrong tag is harmless — but
///         sticking to the canonical set keeps the invariant coverage honest.
///     </para>
/// </summary>
public static class VoicingTagEnricher
{
    /// <summary>
    ///     Returns derived tags for a voicing. Call-site merges these into the existing
    ///     <see cref="VoicingCharacteristics.SemanticTags"/> list before passing to the
    ///     musical-embedding generator.
    /// </summary>
    /// <param name="characteristics">Output of <c>VoicingHarmonicAnalyzer.Analyze</c>.</param>
    /// <param name="sortedMidiNotes">MIDI notes ascending. Empty = no register tag emitted.</param>
    public static IEnumerable<string> Enrich(
        VoicingCharacteristics characteristics,
        IReadOnlyList<int> sortedMidiNotes)
    {
        if (characteristics is null) yield break;

        // ── Register band from mean MIDI ──────────────────────────────────
        if (sortedMidiNotes is { Count: > 0 })
        {
            yield return ClassifyRegister(sortedMidiNotes);
        }

        // ── Mood: consonance × dissonance × quality ───────────────────────
        foreach (var moodTag in ClassifyMood(characteristics, sortedMidiNotes))
        {
            yield return moodTag;
        }

        // ── Style: chord-quality family × voicing pattern ─────────────────
        foreach (var styleTag in ClassifyStyle(characteristics))
        {
            yield return styleTag;
        }

        // ── Technique derived from structural flags ───────────────────────
        foreach (var techTag in ClassifyTechnique(characteristics))
        {
            yield return techTag;
        }
    }

    /// <summary>
    ///     MIDI-mean-based register classification. Band edges are tuned so guitar
    ///     open position (~E3–E4, mean ~55) reads as "mid-low", bebop comping region
    ///     (~G3–G4, mean ~62) reads as "mid", and chord-melody top strings (~E4–E5,
    ///     mean ~70+) reads as "mid-high" or "high".
    /// </summary>
    private static string ClassifyRegister(IReadOnlyList<int> midi)
    {
        double sum = 0;
        for (var i = 0; i < midi.Count; i++) sum += midi[i];
        var mean = sum / midi.Count;

        return mean switch
        {
            < 50 => "register:low",
            < 58 => "register:mid-low",
            < 67 => "register:mid",
            < 76 => "register:mid-high",
            _    => "register:high"
        };
    }

    /// <summary>
    ///     Consonance/brightness-driven mood. Uses the analyzer's already-computed
    ///     <see cref="VoicingCharacteristics.Consonance"/> plus chord-quality hints from
    ///     <c>ChordIdentification</c>. Deliberately conservative: emits at most two mood
    ///     tags per voicing to avoid skewing the SYMBOLIC partition toward a single
    ///     voicing's tag density.
    /// </summary>
    private static IEnumerable<string> ClassifyMood(VoicingCharacteristics c, IReadOnlyList<int> midi)
    {
        var q = (c.ChordId.Quality ?? "").ToLowerInvariant();
        var isMinor = q.Contains("min") || q.StartsWith("m", StringComparison.Ordinal) && !q.StartsWith("maj", StringComparison.Ordinal);
        var isMajor = q.Contains("maj") || (!isMinor && !q.Contains("dim") && !q.Contains("aug"));
        var isExtended = q.Contains("maj7") || q.Contains("maj9") || q.Contains("9") || q.Contains("11") || q.Contains("13");
        var hasDrop = c.DropVoicing is not null;
        var midiMean = midi.Count > 0 ? midi.Average() : 0;

        // "tense" — high dissonance overrides everything
        if (c.DissonanceScore > 0.65)
        {
            yield return "tense";
            yield break;
        }

        // "dreamy" — extended + open/drop voicings in mid-to-high register
        if (isExtended && (hasDrop || c.IsOpenVoicing) && midiMean >= 58)
        {
            yield return "dreamy";
        }

        // "bright" — major + mid-high to high register + consonant
        if (isMajor && midiMean >= 67 && c.Consonance >= 0.60)
        {
            yield return "bright";
        }

        // "melancholy" / "sad" — minor + low-to-mid register
        if (isMinor && midiMean < 60)
        {
            yield return "melancholy";
            yield return "sad";
        }

        // "stable" — high consonance, triadic, closed voicing
        if (c.Consonance >= 0.75 && c.NoteCount <= 4 && !c.IsOpenVoicing && !hasDrop)
        {
            yield return "stable";
        }

        // "resonant" — wide spread + open voicing (lots of ringing overtones)
        if (c.IsOpenVoicing && c.IntervalSpread >= 14)
        {
            yield return "resonant";
        }
    }

    /// <summary>
    ///     Style tags from chord-family heuristics. Under-tagging is preferred to over-
    ///     tagging: a voicing tagged "jazz" should at least be plausibly jazz.
    ///     Relaxed 2026-04-19: any extended-quality chord (7/9/11/13/maj7/m7/m7b5) gets
    ///     "jazz" — the live MCP battery showed block-chord Cmaj7 was excluded and the
    ///     `"Cmaj7 jazz"` query tied with bare `"Cmaj7"` as a result. Plain triads still
    ///     don't get tagged. Drop-2/drop-3/rootless structures carry the stronger jazz
    ///     signal via their own canonical tags (drop-2-voicings, rootless, shell-voicing),
    ///     so this relaxation doesn't lose specificity.
    /// </summary>
    private static IEnumerable<string> ClassifyStyle(VoicingCharacteristics c)
    {
        var q = (c.ChordId.Quality ?? "").ToLowerInvariant();
        var drop = c.DropVoicing?.ToLowerInvariant() ?? "";

        var isExtended =
            q.Contains("maj7") || q.Contains("maj9") ||
            q.Contains("m7")   || q.Contains("min7") || q.Contains("min9") ||
            q.Contains("7")    || q.Contains("9")    || q.Contains("11") || q.Contains("13") ||
            q.Contains("m7b5");

        // Jazz: any extended quality qualifies. Voicing-pattern tags (drop-*, shell-*,
        // rootless) supply the finer style differentiation.
        if (isExtended)
        {
            yield return "jazz";
        }

        // Neo-soul: rootless extended chords in mid/mid-high register (approximated
        // here by the structural proxies).
        if (c.IsRootless && isExtended)
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
        var isSimpleTriad = c.NoteCount <= 5 && (q == "maj" || q == "" || q == "m" || q == "min");
        if (isSimpleTriad && c.IsOpenVoicing)
        {
            yield return "campfire-chord";
        }
    }

    /// <summary>Technique tags derived from structural flags.</summary>
    private static IEnumerable<string> ClassifyTechnique(VoicingCharacteristics c)
    {
        if (c.IsRootless) yield return "rootless";

        // closed / open voicing — canonical bit in the vocabulary.
        yield return c.IsOpenVoicing ? "open-voicing" : "closed-voicing";

        // Drop-voicing canonical form. VoicingCharacteristics stores e.g. "Drop-2";
        // the canonical registry key is "drop-2-voicings".
        if (c.DropVoicing is { } d)
        {
            var norm = d.ToLowerInvariant().Replace(" ", "-");
            if (norm.Contains("drop-2"))       yield return "drop-2-voicings";
            else if (norm.Contains("drop-3"))  yield return "drop-3-voicings";
        }

        // Shell voicing heuristic: exactly 3 notes with no root doubling and at least
        // one 3rd-or-7th present. Without direct interval inspection here we approximate
        // by "rootless + 3 notes" which covers the canonical jazz shell.
        if (c.IsRootless && c.NoteCount == 3)
        {
            yield return "shell-voicing";
        }
    }
}
