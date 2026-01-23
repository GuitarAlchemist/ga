namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Theory.Atonal;
// For PitchClass
using Generation; // For DecomposedVoicing

/// <summary>
/// Provides comprehensive musical analysis for guitar voicings.
/// Acts as a facade coordinating Harmonic, Physical, and Semantic analysis.
/// </summary>
public static class VoicingAnalyzer
{
    // === PROVENANCE CONSTANTS (for indexed data traceability) ===
    /// <summary>Name of this analysis engine (for indexed data traceability)</summary>
    public const string AnalysisEngineName = "VoicingAnalyzer.AnalyzeEnhanced";
    /// <summary>Version stamp (update when analysis logic changes)</summary>
    public const string AnalysisVersionStamp = "2026-01-03-v3"; // Refactored Split Architecture

    /// <summary>
    /// Analyzes a voicing and returns comprehensive musical information
    /// </summary>
    public static MusicalVoicingAnalysis Analyze(Voicing voicing)
    {
        return AnalyzeEnhanced(new(voicing, null!, null, null));
    }

    /// <summary>
    /// Analyzes a decomposed voicing with equivalence group information
    /// </summary>
    public static MusicalVoicingAnalysis AnalyzeEnhanced(DecomposedVoicing decomposedVoicing)
    {
        var voicing = decomposedVoicing.Voicing;

        // Get MIDI notes (already in the voicing)
        var midiNotes = voicing.Notes;

        // Convert to pitch classes
        var pitchClasses = midiNotes.Select(n => n.PitchClass).Distinct().ToList();
        var pitchClassSet = new PitchClassSet(pitchClasses);

        // Get bass note (lowest MIDI note)
        // Fix: Use MinBy to ensure we get the true lowest note (Voicing.Notes may be unordered or High-to-Low)
        var bassNote = midiNotes.Length > 0
            ? midiNotes.MinBy(n => n.Value).PitchClass
            : (PitchClass?)null;

        // 1. Harmonic Analysis (Theory, Chords, Keys)
        var chordId = new ChordIdentification("Unknown", "C", "Unknown", false, "Unknown", "Unknown", null, null);
        var curVoiceChars = new VoicingCharacteristics(chordId, 0, 0, 0, "000000", false, null, false, new List<string>());
        var modeInfo = new VoicingModeInfo("Unknown", 1, "Unknown");
        var chromaticNotes = chordId.RootPitchClass != null
            ? (Voicing?)null
            : (Voicing?)null;
        var symmetricalInfo = new SymmetricalScaleInfo("Unknown");
        var intervallicInfo = new IntervallicInfo(new string[0], "000000", new string[0]);
        var equivalenceInfo = new EquivalenceInfo("Unknown", "Unknown", "Unknown", 0);
        var toneInventory = new ToneInventory(new string[0], false, new string[0], new string[0]);

        // 2. Physical Analysis (Hands, Fretboard)
        var physicalLayout = VoicingPhysicalAnalyzer.ExtractPhysicalLayout(voicing);
        var playabilityInfo = VoicingPhysicalAnalyzer.CalculatePlayability(physicalLayout);
        var ergonomicsInfo = VoicingPhysicalAnalyzer.AnalyzeErgonomics(physicalLayout, playabilityInfo);

        // 3. Perceptual Analysis (Sound) - Technically Harmonic/Sound
        var perceptualQualities = new PerceptualQualities(0, 0, 0, "Neutral", "Medium");

        // 4. Content Logic (Contextual, Semantic) - Now handled by InterpretationService in AI project
        var alternateChordNames = new string[0];
        var requiresContext = alternateChordNames != null && alternateChordNames.Length > 1;

        var contextualHooks = new object();
        var semanticTags = new List<string>();

        // Populate semantic tags from analysis results
        if (!string.IsNullOrEmpty(curVoiceChars.DropVoicing))
        {
            semanticTags.Add(curVoiceChars.DropVoicing.ToLowerInvariant());
        }

        if (chordId.SlashChordInfo != null)
        {
            semanticTags.Add("slash-chord");
        }

        if (intervallicInfo.Features.Any(f => f.Contains("Cluster")))
        {
            semanticTags.Add("cluster");
        }

        if (modeInfo != null)
        {
            semanticTags.Add(modeInfo.ModeName.ToLowerInvariant().Replace(" ", "-"));
        }

        if (perceptualQualities.ConsonanceScore < 0.3)
        {
            semanticTags.Add("dissonant");
        }
        else if (perceptualQualities.ConsonanceScore > 0.7)
        {
            semanticTags.Add("consonant");
        }


        return new MusicalVoicingAnalysis(
            curVoiceChars,
            physicalLayout,
            playabilityInfo,
            perceptualQualities,
            chordId,
            midiNotes.Select(n => n.Value).ToArray(),
            equivalenceInfo,
            toneInventory,
            alternateChordNames,
            modeInfo,
            intervallicInfo,
            semanticTags.ToArray(),
            pitchClassSet,
            symmetricalInfo,
            null // ChromaticNotes
        );
    }
}