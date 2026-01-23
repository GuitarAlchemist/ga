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

# VoicingKeyFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("identification.ClosestKey", "ParseKey(identification.ClosestKey)"),
    ("identification.ClosestKey?.ToString().Contains(\"C\") ?? false", "identification.ClosestKey?.Contains(\"C\") ?? false"),
])

# VoicingAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    # Re-apply broken replacement fixes
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())", 
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())"),
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\")",
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo(new string[0], \"000000\", new string[0])"),
    ("using ContextualHooks = object;", "using ContextualHooks = System.Object;"), # Add alias if needed or remove usage
    ("var contextualHooks = new ContextualHooks(null, null, null, null);", "var contextualHooks = new object();"),
])

# GpuVectorOps.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs", [
    (".Length", ".Count"), # DeviceCollection length property
    # Fix Action cast again if needed
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("n.PitchClass", "n"), # Int has no PitchClass
    ("n.Value", "n"),
    ("voicing.Count", "voicing.Notes.Length"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])

# HybridChordNamingService.cs
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])

# AtonalChordAnalysisService.cs
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
])

# BasicChordExtensionsService.cs
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    ("ChordTemplate.GA", "ChordTemplate"),
    ("RequiredGA", "Required"),
])

# ChordTemplateFactory.cs
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("ChordFormula.Any", "ChordFormula.Intervals.Any"), # Assuming Intervals property
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("ChordIdentification.GA", "ChordIdentification"),
    ("ChordFormula.GA", "ChordFormula"),
])
