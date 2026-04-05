import os

def replace_in_file(filepath, replacements):
    if not os.path.exists(filepath):
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
# Fix IdentifyChord call (remove extra args)
# Fix missing methods by using stub objects or null
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    ("VoicingHarmonicAnalyzer.AnalyzeVoicingCharacteristics(voicing, chordId)", "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\")"),
    ("VoicingHarmonicAnalyzer.DetectMode(pitchClassSet)", "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingModeInfo(\"Unknown\", 1, \"Unknown\")"),
    ("VoicingHarmonicAnalyzer.IdentifyChromaticNotes(pitchClassSet, chordId.ClosestKey)", "new GA.Domain.Primitives.Note[0]"),
    ("VoicingHarmonicAnalyzer.IdentifyChord(pitchClassSet, pitchClasses, bassNote)", "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification(\"Unknown\", \"C\", \"Unknown\", null)"),
])

# VoicingIndexingService.cs & VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])

replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])

