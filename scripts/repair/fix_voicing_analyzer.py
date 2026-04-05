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
# The previous search/replace might have left lines in a bad state (e.g. "// var x = ..." without semicolon if stripped)
# Or "var x = // method call" which is invalid.
# Based on errors: "; expected", "Invalid expression term ':'"
# It seems I commented out method calls but left variable assignments dangling?
# e.g. "var x = // VoicingHarmonicAnalyzer.Method(...)" -> "var x = ;" (invalid)
# I should replace the whole line with a default value.

replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    ("var chromaticNotes = // VoicingHarmonicAnalyzer.IdentifyChromaticNotes(decomposed.MidiNotes);", "var chromaticNotes = new GA.Domain.Primitives.Note[0];"),
    ("var symmetricalInfo = // VoicingHarmonicAnalyzer.DetectSymmetricalScales(pcSet);", "var symmetricalInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.SymmetricalScaleInfo(\"Unknown\");"),
    ("var intervallicInfo = // VoicingHarmonicAnalyzer.AnalyzeIntervallic(decomposed.MidiNotes);", "var intervallicInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\");"),
    ("var equivalenceInfo = // VoicingHarmonicAnalyzer.ExtractEquivalenceInfo(pcSet);", "var equivalenceInfo = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.EquivalenceInfo(\"Unknown\", \"Unknown\", \"Unknown\", 0);"),
    ("var toneInventory = // VoicingHarmonicAnalyzer.AnalyzeToneInventory(pcSet, chordId);", "var toneInventory = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ToneInventory(new string[0], false, new string[0], new string[0]);"),
    ("var perceptualQualities = // VoicingHarmonicAnalyzer.AnalyzePerceptualQualities(decomposed.MidiNotes);", "var perceptualQualities = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.PerceptualQualities(0, 0, 0, \"Neutral\");"),
    ("var alternateNames = // VoicingHarmonicAnalyzer.DetectAlternateChordNames(pcSet, chordId);", "var alternateNames = new string[0];"),
    ("var chordId = // VoicingHarmonicAnalyzer.IdentifyChord(pcSet, bassMidi);", "var chordId = new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification(\"Unknown\", \"C\", \"Unknown\", false, \"Unknown\", \"Unknown\");"),
    ("identification.RootPitchClass)", "identification.RootPitchClass"),
])
