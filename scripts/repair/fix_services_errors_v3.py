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

# VoicingPhysicalAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingPhysicalAnalyzer.cs", [
    ("FretboardGeometry.", "PhysicalFretboardCalculator."), # Fix missing class
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("GA.Domain.Services.Fretboard.Biomechanics.HandSize", "GA.Domain.Instruments.Biomechanics.HandSize"), # Fix namespace
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("adjacentGA.Domain.Primitives", "adjacent"),
    (".GA.Domain.Primitives.Any()", ".Any()"),
    ("ChordFormula.GA.Domain.Primitives", "ChordFormula"),
    ("adjacent", "adjacentInterval"), # Guessing variable name
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])

# HybridChordNamingService.cs
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])

# BasicChordExtensionsService.cs
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
    ("expectedGA.Domain.Primitives", "expected"),
])

# AtonalChordAnalysisService.cs
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])
