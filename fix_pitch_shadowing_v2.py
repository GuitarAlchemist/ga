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
    # Force FQN for Note static members in Pitch.cs
    ("Note.Chromatic", "GA.Domain.Primitives.Note.Chromatic"),
    ("Note.Sharp", "GA.Domain.Primitives.Note.Sharp"),
    ("Note.Flat", "GA.Domain.Primitives.Note.Flat"),
    ("Note.KeyNote", "GA.Domain.Primitives.Note.KeyNote"),
    ("Note.Accidented", "GA.Domain.Primitives.Note.Accidented"),
    
    # Avoid double prefix if already fixed
    ("GA.Domain.Primitives.GA.Domain.Primitives.Note", "GA.Domain.Primitives.Note")
]

replace_in_file("Common/GA.Domain.Core/Primitives/Pitch.cs", replacements)
