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

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("using Tonal;", "using GA.Domain.Theory.Tonal;"),
])

# PhysicalFretboardCalculator.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Analysis/PhysicalFretboardCalculator.cs", [
    ("using static Primitives.Position;", "using static GA.Domain.Instruments.Primitives.Position;"),
    ("using GA.Domain.Primitives;", "using GA.Domain.Primitives;\nusing GA.Domain.Instruments.Primitives;"),
    ("using static GA.Domain.Primitives.Position;", "using static GA.Domain.Instruments.Primitives.Position;"),
])

# FretboardChordsGenerator.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Engine/FretboardChordsGenerator.cs", [
    ("using FretboardClass = GA.Business.Core.Fretboard.Primitives.Fretboard;", "using FretboardClass = GA.Domain.Instruments.Primitives.Fretboard;"),
    ("using GA.Business.Core.Fretboard.Primitives;", "using GA.Domain.Instruments.Primitives;"),
])

# SymbolicQueryParser.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/SymbolicQueryParser.cs", [
    ("using Config.Configuration;", "using GA.Business.Config.Configuration;"),
    ("using Config;", "using GA.Business.Config;"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using GA.Domain.Primitives.Position", "using GA.Domain.Instruments.Primitives.Position"),
])

