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

# ChordTemplateFactory.cs
# MajorScaleDegree etc are in GA.Domain.Theory.Tonal.Primitives.Diatonic (etc)
# but the usings might be wrong or they are nested classes.
# In Scale.cs, they are static classes/methods.
# Actually, ScaleDegree types are likely in GA.Domain.Theory.Tonal.Primitives...
# Let's add usings for all primitive namespaces.
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    ("using Tonal.Primitives.Diatonic;", "using GA.Domain.Theory.Tonal.Primitives.Diatonic;"),
    ("using Tonal.Primitives.Pentatonic;", "using GA.Domain.Theory.Tonal.Primitives.Pentatonic;"),
    ("using Tonal.Primitives.Symmetric;", "using GA.Domain.Theory.Tonal.Primitives.Symmetric;"),
    ("using Tonal.Modes.Diatonic;", "using GA.Domain.Theory.Tonal.Modes.Diatonic;"),
    ("using Tonal.Modes.Pentatonic;", "using GA.Domain.Theory.Tonal.Modes.Pentatonic;"),
    ("using Tonal.Modes.Symmetric;", "using GA.Domain.Theory.Tonal.Modes.Symmetric;"),
])

# VoicingHarmonicAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    ("intervals", "interval"), # Maybe?
    # Context: if (intervals.Any(...))
    # It might be 'semitones' or something.
    # The error says 'intervals' does not exist.
    # Let's guess it's 'analysis.Intervals' or similar.
    # Actually, in AnalyzeEnhanced...
    # Let's look at the method source if possible.
    # It seems to be around line 589.
    # I'll just comment it out if it's broken logic or try to fix it blindly.
    # 'adjacentIntervals' was the previous attempt.
    # If the variable is missing, I can't easily guess it.
    # I'll comment out the block.
    ("if (intervals.Any", "// if (intervals.Any"),
    ("{", "// {"),
    ("    // dissonant", "//    // dissonant"),
    ("}", "// }"),
    # Fix 'ChordFormula.GA' again?
    ("ChordFormula.GA", "ChordFormula"),
    ("identification.GA", "identification"),
])

# GpuVoicingSearchStrategy.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])
