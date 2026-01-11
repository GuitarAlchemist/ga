namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using System.Collections.Generic;
using System.Linq;
using Core;
using Atonal; // For PitchClass
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
        var chordId = VoicingHarmonicAnalyzer.IdentifyChord(pitchClassSet, pitchClasses, bassNote);
        var curVoiceChars = VoicingHarmonicAnalyzer.AnalyzeVoicingCharacteristics(voicing, chordId);
        var modeInfo = VoicingHarmonicAnalyzer.DetectMode(pitchClassSet);
        var chromaticNotes = chordId.ClosestKey != null
            ? VoicingHarmonicAnalyzer.IdentifyChromaticNotes(pitchClassSet, chordId.ClosestKey)
            : null;
        var symmetricalInfo = VoicingHarmonicAnalyzer.DetectSymmetricalScales(pitchClassSet);
        var intervallicInfo = VoicingHarmonicAnalyzer.AnalyzeIntervallic(pitchClassSet);
        var equivalenceInfo = VoicingHarmonicAnalyzer.ExtractEquivalenceInfo(decomposedVoicing);
        var toneInventory = VoicingHarmonicAnalyzer.AnalyzeToneInventory(midiNotes, chordId);
        
        // 2. Physical Analysis (Hands, Fretboard)
        var physicalLayout = VoicingPhysicalAnalyzer.ExtractPhysicalLayout(voicing);
        var playabilityInfo = VoicingPhysicalAnalyzer.CalculatePlayability(physicalLayout);
        var ergonomicsInfo = VoicingPhysicalAnalyzer.AnalyzeErgonomics(physicalLayout, playabilityInfo);
        
        // 3. Perceptual Analysis (Sound) - Technically Harmonic/Sound
        var perceptualQualities = VoicingHarmonicAnalyzer.AnalyzePerceptualQualities(midiNotes, physicalLayout);

        // 4. Content Logic (Contextual, Semantic) - Now handled by InterpretationService in AI project
        var alternateChordNames = VoicingHarmonicAnalyzer.DetectAlternateChordNames(pitchClassSet, chordId);
        var requiresContext = alternateChordNames != null && alternateChordNames.Count > 1;

        var contextualHooks = new ContextualHooks(null, null, null, null);
        var semanticTags = new List<string>();

        return new(
            // Core
            midiNotes,
            pitchClassSet,
            
            // Layer 1: Identity
            chordId,
            alternateChordNames,
            requiresContext,
            modeInfo,
            equivalenceInfo,
            
            // Layer 2: Sound
            curVoiceChars,
            toneInventory,
            perceptualQualities,
            symmetricalInfo,
            intervallicInfo,
            chromaticNotes,
            
            // Layer 3: Hands
            physicalLayout,
            playabilityInfo,
            ergonomicsInfo,
            
            // Contextual
            semanticTags,
            contextualHooks
        );
    }
}
