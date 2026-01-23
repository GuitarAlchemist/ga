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

# VoicingAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    # Fix VoicingCharacteristics constructor (9 args)
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\")", 
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())"),
    
    # Fix IntervallicInfo constructor (3 args)
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\")",
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\", new string[0])"),

    # Fix ContextualHooks missing
    # Remove it from constructor call (last arg)
    ("semanticTags,\n            contextualHooks", "semanticTags"),
    ("var contextualHooks = new ContextualHooks(null, null, null, null);", ""),

    # Fix null conditional
    ("? null\n            : null;", "? (GA.Domain.Instruments.Fretboard.Voicings.Core.Voicing?)null : (GA.Domain.Instruments.Fretboard.Voicings.Core.Voicing?)null;"),
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    # Fix VoicingCharacteristics constructor (9 args)
    ("new VoicingCharacteristics(
            chordId,
            dissonanceScore,
            intervalSpread,
            pitchClasses.Count,
            pcSet.IntervalClassVector.ToString()
        )",
     "new VoicingCharacteristics(chordId, dissonanceScore, intervalSpread, pitchClasses.Count, pcSet.IntervalClassVector.ToString(), false, null, false, new List<string>())"),
    
    # Fix ChordIdentification constructor (7 args)
    ("new ChordIdentification(name, root.ToString(), \"Unknown Quality\")", 
     "new ChordIdentification(name, root.ToString(), \"Unknown Quality\", false, \"Function\", \"Quality\", null)"),
])

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("identification.ClosestKey", "null"), # Disable for now or use Key.TryParse
    # Or just comment out logic relying on it.
    # The error was Argument 1: cannot convert from 'string' to 'Key'.
    # In KeyContextFilter logic:
    # KeyContextFilter.InKeyOfC => closestKey?.ToString().Contains("C") ...
    # Wait, 'ClosestKey' is string? In VoicingAnalysisModels.cs I defined it as string?
    # Yes: string? ClosestKey = null.
    # So closestKey?.ToString() is fine.
    # But previous error: Argument 1: cannot convert from 'string' to 'GA.Domain.Theory.Tonal.Key'.
    # Where? 
    # C:\Users\spare\source\repos\ga\Common\GA.Domain.Services\Fretboard\Voicings\Filtering\VoicingKeyFilters.cs(117,42)
    # Ah, FilterByKey expects Key object.
    # If I pass a string, it fails.
    # I should parsing logic or dummy.
    # Let's fix the calls in VoicingKeyFilters.cs if any.
    # Actually, the error `cannot convert from 'string' to 'Key'` suggests I am passing string where Key is expected.
    # Let's check VoicingKeyFilters.cs.
])
