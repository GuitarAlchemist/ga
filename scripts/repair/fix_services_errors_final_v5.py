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

# AtonalChordAnalysisService.cs
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    ("template.GA(", "template.Intervals.Select("),
    (".GA(", ".Intervals.Select("),
    # If it was template.GA.Domain... then:
    ("template.GA.Domain.Primitives.Any()", "template.Intervals.Any()"),
])

# BasicChordExtensionsService.cs
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    ("expectedExtensionsIntervalsExtensions", "expectedExtensionsIntervals"), # Fix double suffix
    ("template.GA.Domain.Primitives.Select", "template.Intervals.Select"),
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("formula.GA.Domain.Primitives.Any()", "formula.Intervals.Any()"),
    ("formula.Intervals.Any().Any()", "formula.Intervals.Any()"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])

# HybridChordNamingService.cs
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    ("template.NoteCount <= 4", "template.NoteCount <= 4"), # Already fixed?
    # Error: Operator '<=' cannot be applied to 'ChordTemplate' and 'int'
    # My previous fix was: template.NoteCount <= 4
    # But maybe I replaced it wrongly.
    # Original line might be: if (template <= 4)
    # So replace `template <= 4` with `template.NoteCount <= 4`
    ("template <= 4", "template.NoteCount <= 4"),
])

# VoicingAnalyzer.cs
# Fix 'var' error? 
# "The name 'var' does not exist in the current context"
# This usually happens if 'var' keyword is used where type is expected or something weird.
# Line 79: var contextualHooks = ...
# Maybe preceding line is malformed?
# Line 77: Error CS0019: Operator '>' cannot be applied...
# analysis.AlternateChordNames.Length > 0 ?
# Wait, I replaced .Count > 0 with .Length > 0.
# If AlternateChordNames is string[], .Length is correct.
# Error says `method group` and `int`.
# That means `Length` was treated as a method group? No, Length is property on Array.
# Unless it's `Enumerable.Count`?
# Maybe `analysis.AlternateChordNames` is not array but List? 
# In VoicingAnalysisModels.cs: `string[] AlternateChordNames`.
# string[] has Length property.
# Wait, maybe I replaced `.Count` with `.Length` but didn't remove `()`? 
# If original was `.Count()`, then `.Length()` is invalid.
# But `.Length` is property.
# If original was `.Count`, `.Length` is property.
# Let's check `VoicingAnalyzer.cs` content around line 77.