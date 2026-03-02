namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using Business.Core.Analysis.Voicings;
using Generation;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Theory.Atonal.Abstractions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal.Modes;
using GA.Domain.Core.Theory.Tonal.Modes.Diatonic;
using GA.Domain.Core.Theory.Tonal.Modes.Pentatonic;
using GA.Domain.Core.Theory.Tonal.Modes.Symmetric;
using GA.Core.Abstractions;
using GA.Domain.Core.Theory.Tonal.Primitives.Diatonic;
using GA.Domain.Core.Theory.Tonal.Primitives.Pentatonic;

/// <summary>
///     Provides comprehensive musical analysis for guitar voicings.
///     Acts as a facade coordinating Harmonic, Physical, and Semantic analysis.
/// </summary>
public static class VoicingAnalyzer
{
    private record ModeLookupResult(string Name, int Degree);

    private static readonly Lazy<Dictionary<PitchClassSetId, ModeLookupResult>> _modeLookupById = new(() =>
    {
        var dict = new Dictionary<PitchClassSetId, ModeLookupResult>();

        void AddModes<TScaleDegree>(IEnumerable<TScaleDegree> degrees, Func<TScaleDegree, ScaleMode> getMode)
            where TScaleDegree : IValueObject
        {
            foreach (var degree in degrees)
            {
                var mode = getMode(degree);
                var root = mode.Notes.First().PitchClass;
                var pcs = new PitchClassSet(mode.Notes.Select(n => n.PitchClass - root));
                if (!dict.ContainsKey(pcs.Id))
                {
                    dict[pcs.Id] = new ModeLookupResult(mode.Name, degree.Value);
                }
            }
        }

        AddModes(MajorScaleDegree.Items, d => MajorScaleMode.Get(d));
        AddModes(HarmonicMinorScaleDegree.Items, d => HarmonicMinorMode.Get(d));
        AddModes(MelodicMinorScaleDegree.Items, d => MelodicMinorMode.Get(d));
        AddModes(NaturalMinorScaleDegree.Items, d => NaturalMinorMode.Get(d));
        AddModes(HarmonicMajorScaleDegree.Items, d => HarmonicMajorScaleMode.Get(d));
        AddModes(MajorPentatonicScaleDegree.Items, d => MajorPentatonicMode.Get(d));

        // Symmetrical scales (usually 1 mode or handled differently, but we can add them)
        foreach (var mode in WholeToneScaleMode.Items)
        {
            var root = mode.Notes.First().PitchClass;
            var pcs = new PitchClassSet(mode.Notes.Select(n => n.PitchClass - root));
            if (!dict.ContainsKey(pcs.Id)) dict[pcs.Id] = new ModeLookupResult(mode.Name, 1);
        }

        return dict;
    });

    private static readonly Lazy<Dictionary<IntervalClassVector, string>> _familyNames = new(() =>
    {
        var dict = new Dictionary<IntervalClassVector, string>();

        void AddFamily(ScaleMode mode, string name)
        {
            var root = mode.Notes.First().PitchClass;
            var pcs = new PitchClassSet(mode.Notes.Select(n => n.PitchClass - root));
            dict[pcs.IntervalClassVector] = name;
        }

        AddFamily(MajorScaleMode.Get(1), "Major Scale Family");
        AddFamily(HarmonicMajorScaleMode.Get(1), "Harmonic Major Family");
        AddFamily(HarmonicMinorMode.Get(1), "Harmonic Minor Family");
        AddFamily(MelodicMinorMode.Get(1), "Melodic Minor Family");

        return dict;
    });

    // === PROVENANCE CONSTANTS (for indexed data traceability) ===
    /// <summary>Name of this analysis engine (for indexed data traceability)</summary>
    public const string AnalysisEngineName = "VoicingAnalyzer.AnalyzeEnhanced";

    /// <summary>Version stamp (update when analysis logic changes)</summary>
    public const string AnalysisVersionStamp = "2026-01-25-v4"; // Restored real sub-analyzer calls

    /// <summary>
    ///     Analyzes a voicing and returns comprehensive musical information
    /// </summary>
    public static MusicalVoicingAnalysis Analyze(Voicing voicing) => AnalyzeEnhanced(new(voicing, null!, null, null));

    /// <summary>
    ///     Analyzes a decomposed voicing with equivalence group information
    /// </summary>
    public static MusicalVoicingAnalysis AnalyzeEnhanced(DecomposedVoicing decomposedVoicing)
    {
        var voicing = decomposedVoicing.Voicing;

        // Get MIDI notes (already in the voicing)
        var midiNotes = voicing.Notes;

        // Convert to pitch classes
        var pitchClasses = midiNotes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();
        var pitchClassSet = new PitchClassSet(pitchClasses);

        // 1. Harmonic Analysis (Theory, Chords, Keys)
        var curVoiceChars = VoicingHarmonicAnalyzer.Analyze(voicing);
        var chordId = curVoiceChars.ChordId;

        // Modal Family Detection
        VoicingModeInfo? modeInfo = null;
        if (pitchClasses.Count >= 4 && ModalFamily.TryGetValue(pitchClassSet.IntervalClassVector, out var family) && family.Modes.Count > 1)
        {
            var rotationIndex = family.ModeIds.IndexOf(pitchClassSet.Id);
            if (rotationIndex >= 0)
            {
                // Try to get traditional mode name and degree, fallback to generic
                var modeName = $"Mode {rotationIndex + 1}";
                var degreeInFamily = rotationIndex + 1;

                if (_modeLookupById.Value.TryGetValue(pitchClassSet.Id, out var lookup))
                {
                    modeName = lookup.Name;
                    if (lookup.Degree > 0) degreeInFamily = lookup.Degree;
                }

                // Try to get traditional family name, fallback to vector ID
                if (!_familyNames.Value.TryGetValue(pitchClassSet.IntervalClassVector, out var familyName))
                {
                    familyName = $"Family {pitchClassSet.IntervalClassVector.Id}";
                }

                modeInfo = new VoicingModeInfo(
                    modeName,
                    degreeInFamily,
                    familyName,
                    degreeInFamily,
                    pitchClasses.Count);
            }
        }

        var sortedMidi = midiNotes.Select(n => n.Value).OrderBy(n => n).ToArray();
        var adjacentIntervals = new List<string>();
        for (var i = 0; i < sortedMidi.Length - 1; i++)
        {
            adjacentIntervals.Add((sortedMidi[i + 1] - sortedMidi[i]).ToString());
        }

        var intervallicFeatures = DetectIntervallicFeatures(sortedMidi);

        var symmetricalInfo = new SymmetricalScaleInfo("Unknown");
        var intervallicInfo = new IntervallicInfo([.. adjacentIntervals], pitchClassSet.IntervalClassVector.ToString(), [.. intervallicFeatures]);
        var equivalenceInfo = new EquivalenceInfo("Unknown", "Unknown", "Unknown", 0);
        var toneInventory = new ToneInventory([], false, [], []);

        // 2. Physical Analysis (Hands, Fretboard)
        var physicalLayout = VoicingPhysicalAnalyzer.ExtractPhysicalLayout(voicing);
        var playabilityInfo = VoicingPhysicalAnalyzer.CalculatePlayability(physicalLayout);
        var ergonomicsInfo = VoicingPhysicalAnalyzer.AnalyzeErgonomics(physicalLayout, playabilityInfo);
        var physicalTags = VoicingPhysicalAnalyzer.GeneratePhysicalTags(physicalLayout, playabilityInfo, ergonomicsInfo);

        // 3. Perceptual Analysis (Sound)
        var perceptualQualities = new PerceptualQualities(curVoiceChars.Consonance, 0, 0, "Neutral", "Medium");

        // 4. Semantic Tags
        var semanticTags = new List<string>(curVoiceChars.SemanticTags);
        semanticTags.AddRange(physicalTags);

        if (modeInfo != null)
        {
            semanticTags.Add(modeInfo.ModeName.ToLowerInvariant().Replace(" ", "-"));
            semanticTags.Add(modeInfo.FamilyName.ToLowerInvariant().Replace(" ", "-"));
        }
        
        if (curVoiceChars.DropVoicing != null)
        {
            semanticTags.Add(curVoiceChars.DropVoicing.ToLowerInvariant());
        }

        return new(
            curVoiceChars,
            physicalLayout,
            playabilityInfo,
            perceptualQualities,
            chordId,
            [.. midiNotes.Select(n => n.Value)],
            equivalenceInfo,
            toneInventory,
            [], // AlternateChordNames
            modeInfo,
            intervallicInfo,
            [.. semanticTags.Distinct()],
            pitchClassSet,
            symmetricalInfo
        );
    }

    private static List<string> DetectIntervallicFeatures(int[] midiNotes)
    {
        var features = new List<string>();
        if (midiNotes.Length < 2) return features;

        // Detect clusters
        var sorted = midiNotes.Distinct().OrderBy(n => n).ToArray();
        var currentClusterStart = 0;
        for (var i = 1; i <= sorted.Length; i++)
        {
            if (i < sorted.Length && sorted[i] == sorted[i - 1] + 1)
            {
                // Continue cluster
                continue;
            }
            else
            {
                // Cluster ended
                var clusterSize = i - currentClusterStart;
                if (clusterSize >= 3)
                {
                    var semitones = sorted[i - 1] - sorted[currentClusterStart];
                    features.Add($"Cluster ({semitones} semitones)");
                }
                currentClusterStart = i;
            }
        }

        return features;
    }
}
