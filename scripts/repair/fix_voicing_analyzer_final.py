import os

def replace_in_file(filepath, replacements):
    if not os.path.exists(filepath):
        print(f"File not found: {filepath}")
        return
        
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    new_content = content
    for old, new in replacements:
        new_content = new_content.replace(old, new)
    
    if new_content != content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)

# VoicingAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    ("var chromaticNotes = chordId.ClosestKey != null", "var chromaticNotes = chordId.RootPitchClass != null"),
    ("? // VoicingHarmonicAnalyzer.IdentifyChromaticNotes(pitchClassSet, chordId.ClosestKey)", "? null"), # Stub
    (": null;", ": null;"),
    ("var symmetricalInfo = // VoicingHarmonicAnalyzer.DetectSymmetricalScales(pitchClassSet);", "var symmetricalInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.SymmetricalScaleInfo(\"Unknown\");"),
    ("var intervallicInfo = // VoicingHarmonicAnalyzer.AnalyzeIntervallic(pitchClassSet);", "var intervallicInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\");"),
    ("var equivalenceInfo = // VoicingHarmonicAnalyzer.ExtractEquivalenceInfo(decomposedVoicing);", "var equivalenceInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.EquivalenceInfo(\"Unknown\", \"Unknown\", \"Unknown\", 0);"),
    ("var toneInventory = // VoicingHarmonicAnalyzer.AnalyzeToneInventory(midiNotes, chordId);", "var toneInventory = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ToneInventory(new string[0], false, new string[0], new string[0]);"),
    ("var perceptualQualities = // VoicingHarmonicAnalyzer.AnalyzePerceptualQualities(midiNotes, physicalLayout);", "var perceptualQualities = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.PerceptualQualities(0, 0, 0, \"Neutral\");"),
    ("var alternateChordNames = // VoicingHarmonicAnalyzer.DetectAlternateChordNames(pitchClassSet, chordId);", "var alternateChordNames = new string[0];"),
    ("var chordId = // VoicingHarmonicAnalyzer.IdentifyChord(pitchClassSet, pitchClasses, bassNote);", "var chordId = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification(\"Unknown\", \"C\", \"Unknown\", false, \"Unknown\", \"Unknown\");"),
])
