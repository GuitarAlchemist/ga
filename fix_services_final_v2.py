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

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("Primitives.Str", "GA.Domain.Instruments.Primitives.Str"),
    ("Primitives.Fret", "GA.Domain.Instruments.Primitives.Fret"),
    ("Positions.PositionLocation", "GA.Domain.Instruments.Positions.PositionLocation"),
    ("Notes.Primitives.MidiNote", "GA.Domain.Primitives.MidiNote"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("VoicingAnalyzer.", "Analysis.VoicingAnalyzer."), # Assuming namespace is correct now
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"), # Fix broken replace?
])

# Fix broken .GA access
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
    ("expectedGA.Domain.Primitives", "expected"),
])
