namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using GA.Business.Core.Analysis.Voicings;

/// <summary>
///     Thin adapter that maps the runtime carrier <see cref="VoicingCharacteristics" /> onto a
///     <see cref="ChordClassificationContext" /> and delegates to the
///     <see cref="ChordClassificationEngine" /> — the single authority for mood / style / genre /
///     technique tags (Campaign-2 slice C2-#2). The classification rules used to live here; they
///     now live in the engine so the corpus indexer and any future caller share one rule set.
///     Output tags are canonical names known to <c>SymbolicTagRegistry</c>, so the SYMBOLIC
///     embedding partition fires.
/// </summary>
public static class VoicingTagEnricher
{
    /// <summary>
    ///     Returns derived mood/style/technique tags for a voicing. Call-site merges these into the
    ///     existing <see cref="VoicingCharacteristics.SemanticTags" /> list before passing to the
    ///     musical-embedding generator.
    /// </summary>
    /// <param name="characteristics">Output of <c>VoicingHarmonicAnalyzer.Analyze</c>.</param>
    /// <param name="sortedMidiNotes">MIDI notes ascending. Empty = no register tag emitted.</param>
    public static IEnumerable<string> Enrich(
        VoicingCharacteristics characteristics,
        IReadOnlyList<int> sortedMidiNotes)
    {
        if (characteristics is null) return [];

        var ctx = new ChordClassificationContext
        {
            Quality = characteristics.ChordId.Quality ?? "",
            HasSeventhOrBeyond = characteristics.ChordId.HasSeventhOrBeyond,
            Consonance = characteristics.Consonance,
            DissonanceScore = characteristics.DissonanceScore,
            IsRootless = characteristics.IsRootless,
            IsOpenVoicing = characteristics.IsOpenVoicing,
            DropVoicing = characteristics.DropVoicing,
            NoteCount = characteristics.NoteCount,
            IntervalSpread = characteristics.IntervalSpread,
            MeanMidi = sortedMidiNotes is { Count: > 0 } ? sortedMidiNotes.Average() : null
        };

        return ChordClassificationEngine.Classify(ctx);
    }
}
