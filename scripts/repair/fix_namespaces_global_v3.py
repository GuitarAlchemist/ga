import os

def process_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # Mapping table for namespaces
        mappings = [
            # GA prefix variations
            ("using GA.Business.Core.Chords;", "using GA.Domain.Theory.Harmony;\nusing GA.Domain.Services.Chords;"),
            ("using GA.Business.Core.Atonal;", "using GA.Domain.Theory.Atonal;"),
            ("using GA.Business.Core.Tonal;", "using GA.Domain.Theory.Tonal;"),
            ("using GA.Business.Core.Notes;", "using GA.Domain.Primitives;"),
            ("using GA.Business.Core.Intervals;", "using GA.Domain.Primitives;"),
            ("using GA.Business.Core.Primitives;", "using GA.Domain.Primitives;"),
            ("using GA.Business.Core.Fretboard;", "using GA.Domain.Instruments.Fretboard;"),
            ("using GA.Business.Core.Extensions;", "using GA.Domain.Extensions;"),
            ("using GA.Business.Core.Unified;", "using GA.Domain.Unified;"),
            ("using GA.Business.Core.Design;", "using GA.Domain.Design;"),
            ("using GA.Business.Core.Scales;", "using GA.Domain.Theory.Tonal.Scales;"),
            
            # Non-GA prefix variations
            ("using Business.Core.Chords;", "using GA.Domain.Theory.Harmony;\nusing GA.Domain.Services.Chords;"),
            ("using Business.Core.Notes;", "using GA.Domain.Primitives;"),
            ("using Business.Core.Atonal;", "using GA.Domain.Theory.Atonal;"),
            ("using Business.Core.Tonal;", "using GA.Domain.Theory.Tonal;"),
            ("using Business.Core.Intervals;", "using GA.Domain.Primitives;"),
            ("using Business.Core.Primitives;", "using GA.Domain.Primitives;"),
            ("using Business.Core.Fretboard;", "using GA.Domain.Instruments.Fretboard;"),
            ("using Business.Core.Extensions;", "using GA.Domain.Extensions;"),
            ("using Business.Core.Scales;", "using GA.Domain.Theory.Tonal.Scales;"),
            
            # Fretboard sub-namespaces
            ("using GA.Domain.Instruments.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
            ("using GA.Domain.Instruments.Fretboard.Positions", "using GA.Domain.Instruments.Positions"),
            ("using GA.Domain.Instruments.Fretboard.Shapes", "using GA.Domain.Instruments.Shapes"),
            ("using GA.Domain.Instruments.Fretboard.Biomechanics", "using GA.Domain.Instruments.Biomechanics"),
            ("using GA.Domain.Instruments.Fretboard.Fingering", "using GA.Domain.Instruments.Fingering"),
            
            # Service sub-namespaces
            ("using GA.Domain.Instruments.Fretboard.Analysis", "using GA.Domain.Services.Fretboard.Analysis"),
            ("using GA.Domain.Instruments.Fretboard.Voicings", "using GA.Domain.Services.Fretboard.Voicings"),
            
            # Common missing types/namespaces
            ("using Config.Configuration;", "using GA.Business.Config.Configuration;"),
            ("using Config;", "using GA.Business.Config;"),
            
            # Type specific fixes
            ("using GA.Domain.Primitives.Note;", "using GA.Domain.Primitives;"),
            ("using GA.Domain.Primitives.Position;", "using GA.Domain.Instruments.Primitives;"),
            
            # FQN fixes
            ("GA.Business.Core.Chords.ChordTemplateFactory", "GA.Domain.Services.Chords.ChordTemplateFactory"),
            ("GA.Business.Core.Chords", "GA.Domain.Theory.Harmony"),
            ("GA.Business.Core.Tonal", "GA.Domain.Theory.Tonal"),
            ("GA.Business.Core.Atonal", "GA.Domain.Theory.Atonal"),
            ("GA.Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
            
            # Redundant
            ("GA.Domain.Services.Fretboard.Voicings.Analysis.GA.Domain.Instruments.Fretboard.Voicings.Analysis", "GA.Domain.Instruments.Fretboard.Voicings.Analysis"),
        ]
        
        # F# mappings
        fs_mappings = [
            ("open GA.Business.Core.Notes", "open GA.Domain.Primitives"),
            ("open GA.Business.Core.Tonal", "open GA.Domain.Theory.Tonal"),
            ("open GA.Business.Core.Fretboard", "open GA.Domain.Instruments.Fretboard"),
        ]
        
        if filepath.endswith(".cs"):
            for old, new in mappings:
                new_content = new_content.replace(old, new)
            
            if "Tuning" in content and "using GA.Domain.Instruments;" not in new_content:
                 new_content = "using GA.Domain.Instruments;\n" + new_content
        elif filepath.endswith(".fs"):
            for old, new in fs_mappings:
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
