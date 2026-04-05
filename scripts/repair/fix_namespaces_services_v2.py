import os

def replace_in_files(directory, replacements):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                new_content = content
                for old, new in replacements:
                    new_content = new_content.replace(old, new)
                
                if new_content != content:
                    print(f"Updating {filepath}")
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(new_content)

replacements = [
    # Chords split
    ("using GA.Business.Core.Chords;", "using GA.Domain.Theory.Harmony;\nusing GA.Domain.Services.Chords;"),
    ("using Chords;", "using GA.Domain.Theory.Harmony;\nusing GA.Domain.Services.Chords;"),
    
    # Fretboard split
    ("using GA.Business.Core.Fretboard.Primitives;", "using GA.Domain.Instruments.Primitives;"),
    ("using GA.Business.Core.Fretboard.Positions;", "using GA.Domain.Instruments.Positions;"),
    ("using GA.Business.Core.Fretboard.Shapes;", "using GA.Domain.Instruments.Shapes;"),
    ("using GA.Business.Core.Fretboard.Biomechanics;", "using GA.Domain.Instruments.Biomechanics;\nusing GA.Domain.Services.Fretboard.Biomechanics;"),
    ("using GA.Business.Core.Fretboard;", "using GA.Domain.Instruments.Fretboard;\nusing GA.Domain.Services.Fretboard;"),
    
    ("using Fretboard.Primitives;", "using GA.Domain.Instruments.Primitives;"),
    ("using Fretboard.Positions;", "using GA.Domain.Instruments.Positions;"),
    ("using Fretboard.Shapes;", "using GA.Domain.Instruments.Shapes;"),
    ("using Fretboard.Biomechanics;", "using GA.Domain.Instruments.Biomechanics;\nusing GA.Domain.Services.Fretboard.Biomechanics;"),
    ("using Fretboard;", "using GA.Domain.Instruments.Fretboard;\nusing GA.Domain.Services.Fretboard;"),

    # Primitives
    ("using Primitives;", "using GA.Domain.Primitives;\nusing GA.Domain.Instruments.Primitives;"),
    ("using GA.Business.Core.Primitives;", "using GA.Domain.Primitives;"),

    # Others
    ("using Intervals;", "using GA.Domain.Primitives;"),
    ("using Notes;", "using GA.Domain.Primitives;"),
    ("using Atonal;", "using GA.Domain.Theory.Atonal;"),
    ("using Tonal;", "using GA.Domain.Theory.Tonal;"),
    ("using Design;", "using GA.Domain.Design;"),
    
    # Fix broken from previous run
    ("using GA.Business.Core.Atonal", "using GA.Domain.Theory.Atonal"),
    ("using GA.Business.Core.Tonal", "using GA.Domain.Theory.Tonal"),
    ("using GA.Business.Core.Notes", "using GA.Domain.Primitives"),
    
    # Unified
    ("using GA.Business.Core.Unified;", "using GA.Domain.Unified;\nusing GA.Domain.Services.Unified;"),
    ("using Unified;", "using GA.Domain.Unified;\nusing GA.Domain.Services.Unified;"),
]

replace_in_files("Common/GA.Domain.Services", replacements)
