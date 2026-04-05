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

# GpuVectorOps.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    # Revert backend calls if they were wrong. ILGPU 1.5 uses GetCudaDevices()
    # The error 'Context does not contain GetCudaDevices' suggests missing using or assembly reference or wrong version
    # Actually, GetCudaDevices is an extension method in ILGPU.Runtime.Cuda.
    # We added `using ILGPU.Runtime.Cuda;`
    # Let's try `GetCudaDevices` again (maybe I messed up previous replace).
    ("GetCudaBackend", "GetCudaDevices"), 
    ("GetCLBackend", "GetCLDevices"),
    
    # Fix Action field type to match LoadAutoGroupedKernel (no stream)
    ("Action<AcceleratorStream, Index1D", "Action<Index1D"), 
    # Wait, the error CS0029 says:
    # Cannot convert Action<Stream, Index, ...> to Action<Index, ...>
    # This means LoadAutoGroupedStreamKernel returns Action<Stream...>.
    # So my field type `Action<Index1D...>` is wrong if I call `LoadAutoGroupedStreamKernel`.
    # I should change the method call to `LoadAutoGroupedKernel` if I want to use `Action<Index1D...>`.
    ("LoadAutoGroupedStreamKernel", "LoadAutoGroupedKernel"),
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    # Fix broken variable names from previous bad replacements
    ("adjacentIntervals", "intervals"), 
    ("adjacentInterval", "intervals"),
    (".GA.Any()", ".Any()"),
    ("ChordFormula.GA", "ChordFormula"),
    ("identification.GA", "identification"),
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

# VoicingPhysicalAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingPhysicalAnalyzer.cs", [
    ("GA.Domain.Services.Fretboard.Analysis.physicalSpan", "physicalSpan"), # Fix bad replace?
    # Error: 'physicalSpan' does not exist in namespace ...
    # This means I replaced it with FQN but it wasn't a type, it was a variable?
    # Or I replaced a method call result assignment.
    # Error CS0234: The type or namespace name 'physicalSpan' does not exist in the namespace 'GA.Domain.Services.Fretboard.Analysis'
    # Line 96: double spanScore = GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator.GetSpanEffortScore(physicalSpan);
    # This line looks correct if GetSpanEffortScore exists.
    # But GetSpanEffortScore DOES NOT exist in PhysicalFretboardCalculator (error CS0117).
    # So I need to replace that call with inline math.
    ("GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator.GetSpanEffortScore(physicalSpan)", "physicalSpan / 80.0"),
    # Fix previous bad replace
    ("GA.Domain.Services.Fretboard.Analysis.physicalSpan", "physicalSpan"),
])
