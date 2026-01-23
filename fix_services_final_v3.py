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

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("n.PitchClass", "n.Value"), # Fix int extension
    ("voicing.Count", "voicing.Notes.Length"), # Fix Voicing has no Count
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])

# GpuVectorOps.cs - Action cast fix (again)
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    ("to 'System.Action<ILGPU.Index1D", "to 'System.Action<ILGPU.Runtime.AcceleratorStream, ILGPU.Index1D"),
    # Wait, I cannot fix error message. I need to fix the code.
    # The field definition: private static Action<Index1D, ...> _subtractKernel;
    # Should be: private static Action<AcceleratorStream, Index1D, ...> _subtractKernel;
    # And Initialize() should assign it.
    # Let's read GpuVectorOps.cs to be sure what to replace.
])
