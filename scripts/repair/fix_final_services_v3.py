import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # Mapping table for missing GA. prefix or wrong Core. prefix
        mappings = [
            ("using Core.Atonal;", "using GA.Domain.Core.Theory.Atonal;"),
            ("using Core.Tonal;", "using GA.Domain.Core.Theory.Tonal;"),
            ("using Core.Fretboard.Shapes;", "using GA.Domain.Core.Instruments.Shapes;"),
            ("using Core.Notes;", "using GA.Domain.Core.Primitives;"),
            ("using Core.Intervals;", "using GA.Domain.Core.Primitives;"),
            ("using Core.Primitives;", "using GA.Domain.Core.Primitives;"),
            ("using Core.Fretboard;", "using GA.Domain.Core.Instruments.Fretboard;"),
            
            # Non-Core prefixes
            ("using GA.Core.Atonal;", "using GA.Domain.Core.Theory.Atonal;"),
            ("using GA.Core.Tonal;", "using GA.Domain.Core.Theory.Tonal;"),
            ("using GA.Core.Fretboard;", "using GA.Domain.Core.Instruments.Fretboard;"),
            
            # FQN fixes in code
            ("(PitchClassSet ", "(GA.Domain.Core.Theory.Atonal.PitchClassSet "),
            ("(IntervalClassVector ", "(GA.Domain.Core.Theory.Atonal.IntervalClassVector "),
            ("IEnumerable<FretboardShape>", "IEnumerable<GA.Domain.Core.Instruments.Shapes.FretboardShape>"),
            ("Task<IEnumerable<FretboardShape>>", "Task<IEnumerable<GA.Domain.Core.Instruments.Shapes.FretboardShape>>"),
            ("(FretboardShape ", "(GA.Domain.Core.Instruments.Shapes.FretboardShape "),
        ]
        
        for old, new in mappings:
            new_content = new_content.replace(old, new)
            
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run on GA.Business.AI and GA.BSP.Core
directories = ["Common/GA.Business.AI", "Common/GA.BSP.Core"]
for d in directories:
    for root, dirs, files in os.walk(d):
        for file in files:
            if file.endswith(".cs"):
                fix_file(os.path.join(root, file))
