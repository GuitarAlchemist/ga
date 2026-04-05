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
    ("PhysicalFretboardCalculator.GetSpanEffortScore(physicalSpan)", "physicalSpan / 10.0"),
])

# GpuVectorOps.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    # Revert to standard ILGPU names as per context
    ("GetCudaBackend", "GetCudaDevices"), 
    ("GetCLBackend", "GetCLDevices"),
    ("CreateCPUAccelerator(0)", "CreateCPUAccelerator(0)"), # Keep this
    ("AcceleratorStream", "AcceleratorStream"), # Keep
    # Fix delegate cast issue by using explicit cast or matching signature
    # The error says cannot convert Action<Index1D, ...> to Action<Stream, Index1D...>
    # So we should change the field type to match what Load... returns
    ("Action<AcceleratorStream, Index1D", "Action<Index1D"), 
    # And fix invocation
    ("kernel(accelerator.DefaultStream, ", "kernel("),
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("adjacentIntervalss", "adjacentIntervals"),
])
