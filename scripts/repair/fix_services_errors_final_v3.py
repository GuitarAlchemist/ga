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
    ("RequiredGA", "RequiredIntervals"),
    ("expected", "expectedExtensions"), # Assuming variable name
    ("Required", "RequiredIntervals"), # If property name is RequiredIntervals
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("ChordFormula.Any", "ChordFormula.Intervals.Any"), 
])

# VoicingKeyFilters.cs
# Fix string to Key conversion logic
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("identification.ClosestKey", "ParseKey(identification.ClosestKey)"),
])

# VoicingHarmonicAnalyzer.cs & VoicingAnalyzer.cs
# Constructor fixes
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("new ChordIdentification(name, root.ToString(), \"Unknown Quality\")", 
     "new ChordIdentification(name, root.ToString(), \"Unknown Quality\", true, \"Function\", \"Quality\", null, null)"),
])

replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())", 
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())"), # Ensure this matches current constructor
    
    # Fix VoicingCharacteristics usage in VoicingAnalyzer (constructor arg count mismatch?)
    # Constructor has 9 args.
    # Error CS7036: No argument corresponding to 'IsOpenVoicing'.
    # It seems I stubbed it but maybe with wrong args or previous replace failed?
    # Let's ensure exact match or update constructor call.
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification(\"Unknown\", \"C\", \"Unknown\", false, \"Unknown\", \"Unknown\")",
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification(\"Unknown\", \"C\", \"Unknown\", false, \"Unknown\", \"Unknown\", null, null)"),
     
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\")",
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\", new string[0])"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("voicing.Count", "voicing.Notes.Length"),
])

# HybridChordNamingService.cs
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])

# AtonalChordAnalysisService.cs
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])
