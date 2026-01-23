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

# BasicChordExtensionsService.cs
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    ("RequiredGA", "Required"),
    ("expected", "expectedExtensions"), # Assuming variable name
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("using GA.Domain.Primitives.RelativeFret", "using GA.Domain.Instruments.Primitives.RelativeFret"),
    ("using GA.Domain.Primitives.RelativeFretVector", "using GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("using GA.Domain.Primitives.Position", "using GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# AtonalChordAnalysisService.cs & HybridChordNamingService.cs
# Fix 'ChordTemplate.GA' -> 'ChordTemplate'
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("ChordFormula.Any", "ChordFormula.Intervals.Any"), 
])
