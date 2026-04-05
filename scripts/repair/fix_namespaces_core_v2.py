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
    ("using Domain.Primitives.Extensions;", "using GA.Domain.Primitives.Extensions;"),
    ("using Domain.Primitives.Primitives;", "using GA.Domain.Primitives;"),
    ("using Notes.Primitives;", "using GA.Domain.Primitives;"),
    ("using Intervals.Primitives;", "using GA.Domain.Primitives;"),
    ("using static Notes.Note;", "using static GA.Domain.Primitives.Note;"),
    ("using Atonal.Primitives;", "using GA.Domain.Theory.Atonal;"),
    ("using GA.Domain.Tonal.Modes;", "using GA.Domain.Theory.Tonal.Modes;"),
    ("using Modes;", "using GA.Domain.Theory.Tonal.Modes;"),
    ("using Notes.Extensions;", "using GA.Domain.Primitives.Extensions;"),
    ("using Domain.Primitives;", "using GA.Domain.Primitives;"),
    ("using Extensions;", "using GA.Domain.Extensions;"),
    ("using Notes;", "using GA.Domain.Primitives;"),
    ("using Intervals;", "using GA.Domain.Primitives;"),
    ("using Tonal;", "using GA.Domain.Theory.Tonal;")
]

replace_in_files("Common/GA.Domain.Core", replacements)
