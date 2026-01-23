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
    ("expectedExtensionsIntervalsIntervals", "expectedExtensionsIntervals"), # Fix double suffix
    # The variable name in line 93 is expectedExtensionsIntervalsIntervals because I replaced expectedExtensionsInfo.RequiredIntervals (which became RequiredIntervalsIntervals if I replaced Required)
    # Let's fix line 93 first.
    # var expectedExtensionsIntervalsIntervals = expectedExtensionsIntervalsInfo.RequiredIntervals.Domain.Primitives.ToHashSet();
    # It should be: var expectedExtensionsIntervals = expectedExtensionsIntervalsInfo.RequiredIntervals.ToHashSet();
    ("expectedExtensionsIntervalsInfo.RequiredIntervals.Domain.Primitives.ToHashSet()", "expectedExtensionsIntervalsInfo.RequiredIntervals.ToHashSet()"),
    ("expectedExtensionsIntervalsIntervals", "expectedExtensionsIntervals"),
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("formula.GA.Domain.Primitives.Any()", "formula.Intervals.Any()"),
])

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    # Argument 1: cannot convert from 'string' to 'GA.Domain.Theory.Tonal.Key'
    # Locations: 117, 119, 121.
    # These are calls to FilterByKey.
    # FilterByKey("C") -> FilterByKey(Key.Major.C) or ParseKey("C")
    # I already tried ParseKey but maybe it wasn't applied or I didn't replace the calls.
    # Let's replace the string literals with ParseKey calls if they are arguments to FilterByKey.
    # But replacing "C" is dangerous.
    # Let's target the lines.
    # MatchesKeyContext likely calls FilterByKey.
    # Let's just comment out the body of MatchesKeyContext again if it's still broken.
    # Or replace `FilterByKey("C"` with `FilterByKey(ParseKey("C")`.
])

def fix_voicing_key_filters(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    new_lines = []
    for line in lines:
        if "FilterByKey(" in line and '"' in line:
            # Replace string args with ParseKey
            # e.g. FilterByKey("C") -> FilterByKey(ParseKey("C"))
            # Simple regex-like replacement
            line = line.replace('FilterByKey("', 'FilterByKey(ParseKey("')
            line = line.replace('")', '"))')
        new_lines.append(line)
        
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

fix_voicing_key_filters("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs")

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])