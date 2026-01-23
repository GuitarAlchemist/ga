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
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.ChordIdentification", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.ChordIdentification"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.VoicingCharacteristics", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingCharacteristics"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.VoicingModeInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.VoicingModeInfo"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.SymmetricalScaleInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.SymmetricalScaleInfo"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.IntervallicInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.IntervallicInfo"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.EquivalenceInfo", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.EquivalenceInfo"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.ToneInventory", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.ToneInventory"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.PerceptualQualities", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.PerceptualQualities"),
    ("GA.Domain.Services.Fretboard.Voicings.Analysis.MusicalVoicingAnalysis", "GA.Domain.Instruments.Fretboard.Voicings.Analysis.MusicalVoicingAnalysis"),
    ("GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator", "GA.Domain.Services.Fretboard.Analysis.PhysicalFretboardCalculator"), # Correct
    
    # Generic using fixes
    ("using GA.Domain.Instruments.Fretboard.Analysis;", "using GA.Domain.Services.Fretboard.Analysis;"),
    ("using GA.Domain.Instruments.Fretboard.Voicings;", "using GA.Domain.Services.Fretboard.Voicings;"),
]

# Run on all relevant directories
directories = ["Common/GA.Domain.Services", "Apps", "Demos", "Tools", "GA.Data.MongoDB", "Tests"]
for d in directories:
    replace_in_files(d, replacements)
