import os
import re

def process_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # Mapping table for namespaces
        # Key: Old substring to match, Value: New substring
        mappings = [
            ("using GA.Business.Core.Atonal", "using GA.Domain.Theory.Atonal"),
            ("using GA.Business.Core.Tonal", "using GA.Domain.Theory.Tonal"),
            ("using GA.Business.Core.Notes", "using GA.Domain.Primitives"),
            ("using GA.Business.Core.Intervals", "using GA.Domain.Primitives"),
            ("using GA.Business.Core.Primitives", "using GA.Domain.Primitives"),
            ("using GA.Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
            ("using GA.Business.Core.Extensions", "using GA.Domain.Extensions"),
            ("using GA.Business.Core.Unified", "using GA.Domain.Unified"),
            ("using GA.Business.Core.Chords", "using GA.Domain.Theory.Harmony"), # or Services.Chords
            ("using GA.Business.Core.Design", "using GA.Domain.Design"),
            
            # Cases without GA prefix
            ("using Business.Core.Atonal", "using GA.Domain.Theory.Atonal"),
            ("using Business.Core.Tonal", "using GA.Domain.Theory.Tonal"),
            ("using Business.Core.Notes", "using GA.Domain.Primitives"),
            ("using Business.Core.Intervals", "using GA.Domain.Primitives"),
            ("using Business.Core.Primitives", "using GA.Domain.Primitives"),
            ("using Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
            ("using Business.Core.Extensions", "using GA.Domain.Extensions"),
            ("using Business.Core.Unified", "using GA.Domain.Unified"),
            ("using Business.Core.Chords", "using GA.Domain.Theory.Harmony"),
            ("using Business.Core.Design", "using GA.Domain.Design"),
            
            # Fretboard specifics
            ("using GA.Domain.Instruments.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
            ("using GA.Domain.Instruments.Fretboard.Positions", "using GA.Domain.Instruments.Positions"),
            ("using GA.Domain.Instruments.Fretboard.Shapes", "using GA.Domain.Instruments.Shapes"),
            ("using GA.Domain.Instruments.Fretboard.Biomechanics", "using GA.Domain.Instruments.Biomechanics"),
            
            # Broken/Redundant
            ("using GA.Domain.Theory.Atonal.Primitives", "using GA.Domain.Theory.Atonal"),
            ("using GA.Domain.Theory.Tonal.Modes", "using GA.Domain.Theory.Tonal.Modes"), # redundant but safe
            ("using GA.Domain.Theory.Tonal.Scales", "using GA.Domain.Theory.Tonal.Scales"),
            
            # Ensure Note/Pitch mappings
            ("using Notes.Extensions", "using GA.Domain.Primitives.Extensions"),
            ("using static Notes.Note", "using static GA.Domain.Primitives.Note"),
            
            # FQN fixes in code
            ("GA.Business.Core.Chords", "GA.Domain.Theory.Harmony"),
            ("GA.Business.Core.Tonal", "GA.Domain.Theory.Tonal"),
            ("GA.Business.Core.Atonal", "GA.Domain.Theory.Atonal"),
            ("GA.Business.Core.Fretboard", "GA.Domain.Instruments.Fretboard"),
        ]
        
        for old, new in mappings:
            new_content = new_content.replace(old, new)
            
        # Specific fixes for common mismatches
        new_content = new_content.replace("using GA.Domain.Instruments.Fretboard.Analysis;", "using GA.Domain.Services.Fretboard.Analysis;")
        new_content = new_content.replace("using GA.Domain.Instruments.Fretboard.Voicings;", "using GA.Domain.Services.Fretboard.Voicings;")
        
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
        if ".git" in dirs: dirs.remove(".git")
        if "node_modules" in dirs: dirs.remove("node_modules")
        
        for file in files:
            if file.endswith(".cs") or file.endswith(".fs"):
                process_file(os.path.join(root, file))

walk_and_fix(".")
