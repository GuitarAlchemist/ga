import os

def replace_in_file(filepath, replacements):
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
    ("Note.Flat", "GA.Domain.Primitives.Note.Flat"),
    ("Note.Sharp", "GA.Domain.Primitives.Note.Sharp"),
    ("Note.Accidented", "GA.Domain.Primitives.Note.Accidented"),
    ("Note.KeyNote", "GA.Domain.Primitives.Note.KeyNote"),
    ("Note.Natural", "GA.Domain.Primitives.Note.Natural"),
    ("return new(Flat.", "return new(GA.Domain.Primitives.Note.Flat."), # For well-known pitches if they use Flat directly?
    ("return new(Sharp.", "return new(GA.Domain.Primitives.Note.Sharp.")
]

replace_in_file("Common/GA.Domain.Core/Primitives/Pitch.cs", replacements)
