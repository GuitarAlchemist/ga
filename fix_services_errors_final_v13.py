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

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("OfType<Primitives.Position.Played>()", "OfType<GA.Domain.Instruments.Primitives.Position.Played>()"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("using GA.Domain.Instruments.Primitives.RelativeFret", "using GA.Domain.Instruments.Primitives"), # Fix double prefix if using statement was bad
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("HandSize", "GA.Domain.Instruments.Biomechanics.HandSize"),
])
