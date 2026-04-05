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
    ("n.PitchClass", "n.Value"), # Fix int has no PitchClass
    ("voicing.Count", "voicing.Notes.Length"), # Fix Voicing has no Count
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    # Fix Key conversion from string
    # Assuming FindMatchingKeys returns List<KeyMatch>
    # KeyMatch has Key Key property.
    # Where did we pass string?
    # closestKey is string? (from ChordIdentification)
    # But KeyContextFilter logic checks for "C", "G" etc.
    # The error "Argument 1: cannot convert from 'string' to 'GA.Domain.Theory.Tonal.Key'"
    # likely in some method call I can't see fully.
    # Let's try to find where it happens or just comment out if it's the `FilterByKey` call I stubbed/guessed.
    # Line 117, 119, 121.
    # It seems to be inside MatchesKeyContext or similar.
    # Let's just fix `Key.Parse` usage if visible.
    # Or assume it was `closestKey?.ToString()` logic.
    # I'll try to find `FilterByKey` usage.
])

# GpuVectorOps.cs - Action cast fix
# The error says cannot convert Action<AcceleratorStream, ...> to Action<Index1D, ...>
# This means my field type Action<Index1D...> doesn't match LoadAutoGroupedStreamKernel return.
# I should change field back to Action<AcceleratorStream, Index1D...>
# AND ensure I use it correctly.
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    ("Action<Index1D", "Action<AcceleratorStream, Index1D"),
    ("kernel(new Index1D", "kernel(accelerator.DefaultStream, new Index1D"),
    # Fix allocate if needed
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])
