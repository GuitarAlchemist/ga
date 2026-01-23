import os

def replace_in_file(filepath, replacements):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    new_content = content
    for old, new in replacements:
        new_content = new_content.replace(old, new)
    
    if new_content != content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("Tonal.HarmonicFunction", "GA.Domain.Theory.Tonal.HarmonicFunction"),
    ("Tonal.HarmonicFunctionAnalyzer", "GA.Domain.Theory.Tonal.HarmonicFunctionAnalyzer"),
])

# PhysicalFretboardCalculator.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Analysis/PhysicalFretboardCalculator.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("Played.Position", "Position"), # Heuristic
])

# FretboardChordsGenerator.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Engine/FretboardChordsGenerator.cs", [
    ("using Primitives = GA.Business.Core.Fretboard.Primitives;", "using Primitives = GA.Domain.Instruments.Primitives;"),
    ("using GA.Business.Core.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# SymbolicQueryParser.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/SymbolicQueryParser.cs", [
    ("using Config;", "using GA.Business.Config;"),
    ("SymbolicTagRegistry", "GA.Domain.Services.Fretboard.Voicings.Search.SymbolicTagRegistry"), # Assuming it's here?
    # Actually, SymbolicTagRegistry not found. It might be deleted?
    # If not found, maybe I need to find where it is or delete usage.
    # I'll check its location in next step if this fails.
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])
