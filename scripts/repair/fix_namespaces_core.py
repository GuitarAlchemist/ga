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
    ("using GA.Domain.Theory.Atonal.Primitives;", "using GA.Domain.Theory.Atonal;"),
    ("using GA.Domain.Primitives.Primitives;", "using GA.Domain.Primitives;"),
    ("using GA.Business.Core.Tonal;", "using GA.Domain.Theory.Tonal;"), # For EnhancedChordTemplate
    ("using Atonal;", "using GA.Domain.Theory.Atonal;"), # For Shape files
    ("using Tonal;", "using GA.Domain.Theory.Tonal;"), # For Scale files
    ("using Notes;", "using GA.Domain.Primitives;"), # Notes usually refers to GA.Domain.Primitives (Note, MidiNote)
    ("using Intervals;", "using GA.Domain.Primitives;"), # Interval is in Primitives
    ("using Scales;", "using GA.Domain.Theory.Tonal.Scales;")
]

replace_in_files("Common/GA.Domain.Core", replacements)
