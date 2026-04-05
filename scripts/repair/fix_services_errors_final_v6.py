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
    ("RequiredIntervalsIntervals", "RequiredIntervals"),
    ("expectedExtensionsIntervalsExtensions", "expectedExtensionsIntervals"),
    ("template.GA(", "template.Intervals.Select("), # If broken
    ("template.Intervals.Select.Domain.Primitives.Select", "template.Intervals.Select"), # Possible broken chain
])

# AtonalChordAnalysisService.cs
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    ("template.Select", "template.Intervals.Select"),
])

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("ParseKey(identification.ClosestKey)", "ParseKey(identification.ClosestKey)"), # Ensure helper method exists or disable
    # Error: Argument 1: cannot convert from 'string' to 'GA.Domain.Theory.Tonal.Key'
    # This happens in FilterByKey(voicing, KEY).
    # I replaced the call arguments?
    # I need to find the usages.
    # Ah, I replaced `identification.ClosestKey` with `ParseKey(...)` which returns Key.
    # If the error persists, maybe I didn't replace all occurrences or the helper method is private/static?
    # Helper is `private static Key ParseKey(string? keyName)`.
    # Let's ensure it's called correctly.
])

# VoicingAnalyzer.cs - fix 'var' error
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    ("var contextualHooks =", "var contextualHooks ="), # Seems correct, maybe context is broken.
    # Check if lines were merged or broken.
    # "var  = new object();" seen in read_file output previously.
    ("var  = new object();", "var contextualHooks = new object();"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("using GA.Domain.Primitives.RelativeFret", "using GA.Domain.Instruments.Primitives.RelativeFret"),
    ("using GA.Domain.Primitives.RelativeFretVector", "using GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("using GA.Domain.Primitives.Position", "using GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])
