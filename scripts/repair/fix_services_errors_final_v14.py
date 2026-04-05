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

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Instruments.GA.Domain.Instruments", "GA.Domain.Instruments"), # Remove double prefix
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    # Error: 'VoicingSearchFilters' does not contain definition for 'GA'
    # This implies something like `filters.GA...` which is wrong.
    # It probably means I replaced something like `filters.HandSize` with `filters.GA.Domain...` but `HandSize` was a property of `filters`.
    # Wait, `VoicingSearchFilters` record definition?
    # public record VoicingSearchFilters(..., HandSize? HandSize, ...);
    # If I replaced `HandSize` with FQN in the *property access*, e.g. `filters.HandSize`, it became `filters.GA.Domain...`.
    # I should revert this replacement for property access.
    ("filters.GA.Domain.Instruments.Biomechanics.HandSize", "filters.HandSize"),
    ("using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"), # Ensure using is correct
])
