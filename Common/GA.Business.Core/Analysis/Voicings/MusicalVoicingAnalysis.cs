namespace GA.Business.Core.Analysis.Voicings;

using GA.Domain.Core.Theory.Atonal;

public record MusicalVoicingAnalysis(
    VoicingCharacteristics VoicingCharacteristics, 
    PhysicalLayout PhysicalLayout, 
    PlayabilityInfo PlayabilityInfo, 
    PerceptualQualities PerceptualQualities, 
    ChordIdentification ChordId,
    int[] MidiNotes,
    EquivalenceInfo EquivalenceInfo,
    ToneInventory ToneInventory,
    string[] AlternateChordNames,
    VoicingModeInfo ModeInfo,
    IntervallicInfo IntervallicInfo,
    string[] SemanticTags,
    PitchClassSet PitchClassSet,
    SymmetricalScaleInfo? SymmetricalInfo = null,
    int[]? ChromaticNotes = null
);
