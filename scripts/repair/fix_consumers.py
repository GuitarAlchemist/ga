import os

def replace_in_files(directory, replacements):
    if not os.path.exists(directory):
        return
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                try:
                    with open(filepath, 'r', encoding='utf-8') as f:
                        content = f.read()
                    
                    new_content = content
                    for old, new in replacements:
                        new_content = new_content.replace(old, new)
                    
                    if new_content != content:
                        print(f"Updating {filepath}")
                        with open(filepath, 'w', encoding='utf-8') as f:
                            f.write(new_content)
                except Exception as e:
                    print(f"Error processing {filepath}: {e}")

replacements = [
    # Core mappings
    ("using GA.Business.Core.Atonal", "using GA.Domain.Theory.Atonal"),
    ("using GA.Business.Core.Tonal", "using GA.Domain.Theory.Tonal"),
    ("using GA.Business.Core.Notes", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Intervals", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Primitives", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
    ("using GA.Business.Core.Extensions", "using GA.Domain.Extensions"),
    ("using GA.Business.Core.Unified", "using GA.Domain.Unified"),
    
    # Sub-namespaces
    ("using GA.Business.Core.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
    ("using GA.Business.Core.Fretboard.Positions", "using GA.Domain.Instruments.Positions"),
    ("using GA.Business.Core.Fretboard.Shapes", "using GA.Domain.Instruments.Shapes"),
    ("using GA.Business.Core.Fretboard.Analysis", "using GA.Domain.Services.Fretboard.Analysis"),
    
    # Services mappings
    ("using GA.Business.Core.Chords", "using GA.Domain.Services.Chords"),
    ("using GA.Business.Core.Chords.Analysis", "using GA.Domain.Services.Chords.Analysis"),
    
    # Fretboard Service Specifics
    ("using GA.Business.Core.Fretboard.Biomechanics", "using GA.Domain.Services.Fretboard.Biomechanics"),
    ("using GA.Business.Core.Fretboard.Voicings", "using GA.Domain.Services.Fretboard.Voicings"),
    
    # Specific type fixes
    ("ChordTemplate.GA", "ChordTemplate"),
    
    # Analysis Types
    ("MusicalVoicingAnalysis", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.MusicalVoicingAnalysis"),
    ("VoicingCharacteristics", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics"),
    ("PhysicalLayout", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.PhysicalLayout"),
    ("PlayabilityInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.PlayabilityInfo"),
    ("ErgonomicsInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.ErgonomicsInfo"),
    
    # Primitives mapping
    ("using Notes;", "using GA.Domain.Primitives;"),
    ("using Intervals;", "using GA.Domain.Primitives;"),
    ("using Atonal;", "using GA.Domain.Theory.Atonal;"),
    ("using Tonal;", "using GA.Domain.Theory.Tonal;"),
    ("using Primitives;", "using GA.Domain.Primitives;"),
    ("using Fretboard;", "using GA.Domain.Instruments.Fretboard;"),
    ("using Shapes;", "using GA.Domain.Instruments.Shapes;"),
    ("using Positions;", "using GA.Domain.Instruments.Positions;"),
    ("using Fingering;", "using GA.Domain.Instruments.Fingering;"),
]

# Run
directories = ["GA.Data.MongoDB", "Apps/ga-server", "Demos", "Tools", "Experiments", "Tests", "GaCLI", "GaMcpServer"]
for d in directories:
    replace_in_files(d, replacements)
