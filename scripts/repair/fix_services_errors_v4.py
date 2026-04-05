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

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
    ("ChordFormula.GA.Domain.Primitives", "ChordFormula"),
    ("adjacentIntervalGA.Domain.Primitives", "adjacentInterval"),
    ("identification.GA.Domain.Primitives", "identification"), # Guessing
    ("GA.Domain.Primitives.Any()", "Any()"),
    ("identification.GA", "identification"), # Try removing .GA if it's attached
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("VoicingAnalyzer.", "Analysis.VoicingAnalyzer."),
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])

# GpuVectorOps.cs
# Missing 'Allocate' means missing 'using ILGPU.Runtime;' or similar context issue
# It has 'using ILGPU.Runtime;', maybe extension method conflict or missing 'using ILGPU;'?
# It has both.
# Maybe 'Accelerator' type is ambiguous?
# Or the method signature changed in ILGPU 1.5.1?
# 'accelerator.Allocate<T>(length)' is standard.
# Let's check imports in GpuVectorOps.cs again from read_file output.

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
    ("GA.Domain.Services.Fretboard.Biomechanics.HandSize", "GA.Domain.Instruments.Biomechanics.HandSize"),
])
