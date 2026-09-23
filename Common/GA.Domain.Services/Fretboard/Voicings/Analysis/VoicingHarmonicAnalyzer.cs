namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using Business.Core.Analysis;
using Business.Core.Analysis.Voicings;
using Chords;
using GA.Domain.Core.Theory.Harmony;

public static class VoicingHarmonicAnalyzer
{
    public static VoicingCharacteristics Analyze(Voicing voicing)
    {
        var midiNotes = voicing.Notes.Select(n => n.Value).OrderBy(n => n).ToArray();
        var pitchClasses = voicing.Notes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();

        // Basic identification via PitchClassSet
        var pcSet = new PitchClassSet(pitchClasses);
        var chordId = IdentifyChord(pcSet, pitchClasses, PitchClass.FromValue(midiNotes[0]));

        // Calculate consonance/dissonance
        var dissonanceScore = CalculateDissonance(midiNotes);
        var consonance = 1.0 - Math.Min(dissonanceScore / 10.0, 1.0); // Normalize to 0-1

        // Interval spread
        var intervalSpread = midiNotes.Last() - midiNotes.First();

        // Generate semantic tags based on characteristics
        var semanticTags = GenerateSemanticTags(chordId, intervalSpread, consonance);

        var dropVoicing = DetectDropVoicing(midiNotes);

        // Named arguments: positionally, the spread test landed in IsRootless and IsOpenVoicing
        // was always false, so every voicing wider than an octave was tagged rootless.
        return new(
            ChordId: chordId,
            DissonanceScore: dissonanceScore,
            Consonance: consonance,
            IntervalSpread: intervalSpread,
            NoteCount: pitchClasses.Count,
            IntervalClassVector: pcSet.IntervalClassVector.ToString(),
            // CanonicalChordRecognizer only picks roots among the sounding pitch classes, so the
            // identified chord always contains its root; there is no rootless detection here.
            IsRootless: false,
            DropVoicing: dropVoicing,
            IsOpenVoicing: intervalSpread > 12,
            Features: [],
            SemanticTags: semanticTags
        );
    }

    private static string? DetectDropVoicing(int[] midiNotes)
    {
        if (midiNotes.Length < 4) return null;
        var sorted = midiNotes.OrderBy(n => n).ToArray();

        // General Drop-2 detection: Raise the bass note 1 octave.
        // If the resulting set is "closed" (span <= 12), it's identified as a Drop-2 voicing.
        var raisedBass = sorted[0] + 12;
        var newSet = sorted.Skip(1).Append(raisedBass).OrderBy(n => n).ToArray();
        if (newSet.Max() - newSet.Min() <= 12) return "Drop-2";

        return null;
    }

    /// <summary>
    ///     Phase D: Identifies a chord using the content-enumerated
    ///     <see cref="CanonicalChordRecognizer" /> instead of the legacy first-match-wins
    ///     template search. The returned <see cref="ChordIdentification" /> has both the
    ///     legacy string fields (for backward compatibility) AND the Phase C structured
    ///     fields (<c>CanonicalName</c>, <c>SlashSuffix</c>, <c>Alterations</c>, etc.)
    ///     populated with register-invariant data.
    /// </summary>
    public static ChordIdentification IdentifyChord(PitchClassSet pcSet, IEnumerable<PitchClass> notes,
        PitchClass bassNote)
    {
        var result = CanonicalChordRecognizer.Identify(pcSet, bassNote);

        // Compose the legacy ChordName — this preserves display behavior for
        // existing consumers (they still see "C Major 7/E"). New consumers read
        // CanonicalName (invariant) + SlashSuffix (voicing-specific) separately.
        var legacyChordName = result.DisplayName;

        return new ChordIdentification(
            ChordName: legacyChordName,
            RootPitchClass: result.Root ?? bassNote.ToString(),
            HarmonicFunction: AnalysisConstants.FunctionalHarmony,
            IsNaturallyOccurring: result.IsNaturallyOccurring,
            FunctionalDescription: AnalysisConstants.FunctionalHarmony,
            Quality: result.Quality)
        {
            CanonicalName = result.CanonicalName,
            SlashSuffix = result.SlashSuffix,
            Extension = result.Extension,
            Alterations = result.Alterations,
            PatternName = result.PatternName,
            MatchDistance = result.MatchDistance,
        };
    }

    private static string GetNoteName(int pc)
    {
        string[] names = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
        return names[pc % 12];
    }

    private static double CalculateDissonance(int[] midiNotes)
    {
        // Placeholder dissonance calculation (e.g. based on intervals)
        double dissonance = 0;
        for (var i = 0; i < midiNotes.Length; i++)
        {
            for (var j = i + 1; j < midiNotes.Length; j++)
            {
                var interval = Math.Abs(midiNotes[i] - midiNotes[j]);
                if (interval % 12 == 1 || interval % 12 == 6)
                {
                    dissonance += 1.0; // Minor 2nd or Tritone
                }
            }
        }

        return dissonance;
    }

    private static string[] GenerateSemanticTags(ChordIdentification chordId, int intervalSpread, double consonance)
    {
        var tags = new List<string>();
        
        // 1. Basic Harmonic Quality
        if (consonance > 0.7) tags.Add("consonant");
        else if (consonance < 0.3) tags.Add("dissonant");
        
        // 2. Spread/Register
        if (intervalSpread > 24) tags.Add("wide-voicing");
        else if (intervalSpread < 12) tags.Add("close-voicing");
        
        // 3. Quality Tags
        string? quality = null;
        if (!string.IsNullOrEmpty(chordId.Quality))
        {
            quality = chordId.Quality.ToLowerInvariant();
            tags.Add(quality);
            
            // Genre/mood tags jazz, melancholy and bright are owned by
            // ChordClassificationEngine — added via VoicingTagEnricher in
            // VoicingAnalyzer.AnalyzeEnhanced — with register-aware, one-rule-per-tag logic
            // that supersedes the divergent name-substring heuristics that used to live here
            // (Campaign-2 slice C2-#2). The remaining pop/tension/dark/balanced tags are not
            // registry-canonical (fire no SYMBOLIC bit) and are a separate dead-tag cleanup
            // pending a downstream-consumption trace — deliberately left untouched this slice.
            if (quality.Contains("sus2") || quality.Contains("sus4") || quality.Contains("add9"))
                tags.Add("pop");
            if (quality.Contains("diminished") || quality.Contains("augmented") || quality.Contains("alt"))
                tags.Add("tension");
        }

        // 5. Functional Moods (mood 'bright' now owned by ChordClassificationEngine)
        if (consonance < 0.4) tags.Add("dark");
        if (intervalSpread > 12 && intervalSpread < 24) tags.Add("balanced");

        return [.. tags.Distinct()];
    }
}
