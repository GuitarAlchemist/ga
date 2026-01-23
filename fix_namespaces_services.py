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
    ("namespace GA.Business.Core", "namespace GA.Domain.Services"),
    ("using GA.Business.Core.Atonal", "using GA.Domain.Theory.Atonal"),
    ("using GA.Business.Core.Tonal", "using GA.Domain.Theory.Tonal"),
    ("using GA.Business.Core.Notes", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Intervals", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Primitives", "using GA.Domain.Primitives"),
    ("using GA.Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
    ("using GA.Business.Core.Extensions", "using GA.Domain.Extensions"),
    
    # Short forms
    ("using Notes;", "using GA.Domain.Primitives;"),
    ("using Intervals;", "using GA.Domain.Primitives;"),
    ("using Atonal;", "using GA.Domain.Theory.Atonal;"),
    ("using Tonal;", "using GA.Domain.Theory.Tonal;"),
    ("using Primitives;", "using GA.Domain.Primitives;"),
    ("using Fretboard;", "using GA.Domain.Instruments.Fretboard;"),
    ("using Shapes;", "using GA.Domain.Instruments.Shapes;"),
    ("using Positions;", "using GA.Domain.Instruments.Positions;"),
    ("using Fingering;", "using GA.Domain.Instruments.Fingering;"),
    
    # Extensions
    ("using GA.Domain.Primitives.Extensions;", "using GA.Domain.Primitives.Extensions;"), # No change
    ("using Extensions;", "using GA.Domain.Extensions;"),
    
    # Specifics
    ("using GA.Business.Core.Chords;", "using GA.Domain.Services.Chords;"), # Chords logic is in Services
    ("using Chords;", "using GA.Domain.Services.Chords;"),
    
    # Atonal/Tonal sub-namespaces
    ("using Atonal.Primitives;", "using GA.Domain.Theory.Atonal;"),
    ("using Tonal.Scales;", "using GA.Domain.Theory.Tonal.Scales;"),
    ("using Tonal.Modes;", "using GA.Domain.Theory.Tonal.Modes;"),
    ("using Tonal.Primitives;", "using GA.Domain.Theory.Tonal.Primitives;"),
    
    # Fretboard specifics
    ("using Fretboard.Primitives;", "using GA.Domain.Instruments.Primitives;"),
    ("using Fretboard.Positions;", "using GA.Domain.Instruments.Positions;"),
    ("using Fretboard.Shapes;", "using GA.Domain.Instruments.Shapes;"),
    
    # Fix broken references
    ("using GA.Business.Core.Design;", "using GA.Domain.Design;")
]

replace_in_files("Common/GA.Domain.Services", replacements)
