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

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    # Fix VoicingCharacteristics constructor (missing IsOpenVoicing etc?)
    # Record def: (ChordIdentification Identification, double DissonanceScore, int Spread, int NoteCount, string IntervalVector, bool IsOpenVoicing, string? DropVoicing, bool IsRootless, List<string> Features)
    # Current call seems to miss args from error CS7036.
    # It was: new VoicingCharacteristics(chordId, dissonanceScore, intervalSpread, pitchClasses.Count, pcSet.IntervalClassVector.ToString())
    # Should add: false, null, false, new List<string>()
    ("pcSet.IntervalClassVector.ToString()\n        )", "pcSet.IntervalClassVector.ToString(), false, null, false, new List<string>()\n        )"),
    # Same for ChordIdentification if needed?
    # new ChordIdentification(name, root.ToString(), "Unknown Quality")
    # Record: (string ChordName, string RootPitchClass, string HarmonicFunction, bool IsNaturallyOccurring, string FunctionalDescription, string Quality, string? ClosestKey = null, string? SlashChordInfo = null)
    # Missing: HarmonicFunction, IsNaturallyOccurring, FunctionalDescription...
    # Update to: new ChordIdentification(name, root.ToString(), "Unknown", false, "Unknown", "Unknown Quality")
    ("new ChordIdentification(name, root.ToString(), \"Unknown Quality\")", "new ChordIdentification(name, root.ToString(), \"Unknown\", false, \"Unknown\", \"Unknown Quality\")"),
])

# VoicingAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    # Fix VoicingCharacteristics constructor here too
    # It was stubbed as: new ... VoicingCharacteristics(chordId, 0, 0, 0, "000000", false, null, false, new System.Collections.Generic.List<string>()) 
    # Check if this matches. It has 9 args.
    # If error CS7036 persists, maybe I missed one or type mismatch.
    # Error: Argument given that corresponds to required parameter 'IsOpenVoicing'.
    # Maybe parameter order?
    # (Id, Diss, Spread, Count, Vector, IsOpen, Drop, Rootless, Features)
    # My stub: (chordId, 0, 0, 0, "000000", false, null, false, new List...)
    # Looks correct.
    # Maybe previous replace failed or I edited wrong line.
    # Let's try to match the exact string again or relax the match.
    ("new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\")", 
     "new GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics(chordId, 0, 0, 0, \"000000\", false, null, false, new System.Collections.Generic.List<string>())"),
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

# VoicingFilters.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilters.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])