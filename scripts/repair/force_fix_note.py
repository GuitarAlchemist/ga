import os

filepath = "Common/GA.Domain.Core/Primitives/Note.cs"

with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

new_content = content.replace("Intervals.Primitives.", "GA.Domain.Primitives.")
new_content = new_content.replace("key.Notes.Count", "keyNotes.Count")

if new_content != content:
    print("Modifying Note.cs")
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)
else:
    print("No changes made to Note.cs")
