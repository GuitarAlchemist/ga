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
    ("PhysicalFretboardCalculator.", "GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator."), # Fix missing class
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("identification.GA.Domain.Primitives", "identification"),
    ("adjacentIntervalGA.Domain.Primitives", "adjacentInterval"),
    ("adjacentInterval.Any()", "adjacentInterval.ToString().Any()"), # Guessing context? Or just revert
    ("adjacentInterval", "intervals"), # Let's assume the variable name was lost or bad replace.
])

# GpuVectorOps.cs
# The error 'Accelerator does not contain definition for Allocate' usually means generic type inference failed or wrong using.
# It seems Allocate<T> requires 'using ILGPU.Runtime;' which is present.
# Maybe it's 'accelerator.Allocate1D<T>(length)' in newer ILGPU? Or just 'Allocate<T>(length)'.
# Let's try changing Allocate to Allocate1D based on standard ILGPU usage.
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    (".Allocate<", ".Allocate1D<"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("using GA.Domain.Primitives.RelativeFret", "using GA.Domain.Instruments.Primitives.RelativeFret"), # Just remove bad using
    ("using GA.Domain.Primitives.RelativeFretVector", "using GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("using GA.Domain.Primitives.Position", "using GA.Domain.Instruments.Primitives.Position"),
    # Fix explicit types in code if any
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])
