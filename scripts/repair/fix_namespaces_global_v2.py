import os

def process_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # Mapping table for namespaces
        mappings = [
            # Wrong mappings from previous runs
            ("using GA.Domain.Primitives.Primitives;", "using GA.Domain.Primitives;"),
            ("GA.Domain.Primitives.Primitives", "GA.Domain.Primitives"),
            ("GA.Domain.Instruments.Fretboard.Voicings.Filtering", "GA.Domain.Services.Fretboard.Voicings.Filtering"),
            ("GA.Domain.Instruments.Fretboard.Voicings.Generation", "GA.Domain.Services.Fretboard.Voicings.Generation"),
            ("GA.Domain.Instruments.Fretboard.Voicings.Analysis", "GA.Domain.Services.Fretboard.Voicings.Analysis"),
            ("GA.Domain.Instruments.Fretboard.Analysis", "GA.Domain.Services.Fretboard.Analysis"),
            
            # Missing GA prefix ones
            ("using Business.Core.Scales;", "using GA.Domain.Theory.Tonal.Scales;"),
            ("using Business.Core.Tonal;", "using GA.Domain.Theory.Tonal;"),
            ("using Business.Core.Atonal;", "using GA.Domain.Theory.Atonal;"),
            
            # Fix double prefix
            ("GA.Domain.Theory.Atonal.Primitives", "GA.Domain.Theory.Atonal"),
            ("GA.Domain.Theory.Tonal.Primitives", "GA.Domain.Theory.Tonal.Primitives"), # keep
            
            # Fix ChordTemplate ambiguity in consumers
            # If they use ChordTemplate, it's Harmony.ChordTemplate
            ("ChordTemplate = GA.Domain.Theory.Harmony.ChordTemplate", "ChordTemplate = GA.Domain.Theory.Harmony.ChordTemplate"), # noop
            
            # Biomechanics
            ("GA.Domain.Instruments.Fretboard.Biomechanics", "GA.Domain.Instruments.Biomechanics"),
            
            # Fretboard
            ("GA.Domain.Instruments.Fretboard.Primitives", "GA.Domain.Instruments.Primitives"),
            ("GA.Domain.Instruments.Fretboard.Positions", "GA.Domain.Instruments.Positions"),
            ("GA.Domain.Instruments.Fretboard.Shapes", "GA.Domain.Instruments.Shapes"),
        ]
        
        for old, new in mappings:
            new_content = new_content.replace(old, new)
            
        if new_content != content:
            print(f"Fixed {filepath}")
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
                
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

def walk_and_fix(root_dir):
    for root, dirs, files in os.walk(root_dir):
        if "obj" in dirs: dirs.remove("obj")
        if "bin" in dirs: dirs.remove("bin")
        for file in files:
            if file.endswith(".cs") or file.endswith(".fs"):
                process_file(os.path.join(root, file))

walk_and_fix(".")
