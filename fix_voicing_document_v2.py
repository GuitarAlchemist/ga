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

# VoicingDocument.cs
# Error 1: Operator '??' cannot be applied to operands of type 'int' and 'int'
# Location: analysis.ChordId.RootPitchClass != null ? int.Parse(analysis.ChordId.RootPitchClass) : 0 ?? 0)
# 'int.Parse(...) : 0' returns int. '?? 0' is redundant and invalid for int.
# We need to remove the '?? 0' part or Fix the regex that created it.
# The previous replacement was:
# ("analysis.ChordId.RootPitchClass?.Value", "analysis.ChordId.RootPitchClass != null ? int.Parse(analysis.ChordId.RootPitchClass) : 0")
# But maybe the original code had `?? 0` after it?
# Original: `analysis.ChordId.RootPitchClass?.Value ?? 0`
# My replacement result: `analysis.ChordId.RootPitchClass != null ? int.Parse(analysis.ChordId.RootPitchClass) : 0 ?? 0`
# So I should replace `: 0 ?? 0` with `: 0`.

replace_in_file("Common/GA.Domain.Core/Instruments/Fretboard/Voicings/Search/VoicingDocument.cs", [
    (": 0 ?? 0", ": 0"),
    # Error 2: 'List<string>' does not contain a definition for 'Length'
    # analysis.VoicingCharacteristics.Features.Length > 0
    # Features is List<string>. So it should be .Count
    ("analysis.VoicingCharacteristics.Features.Length", "analysis.VoicingCharacteristics.Features.Count"),
])
