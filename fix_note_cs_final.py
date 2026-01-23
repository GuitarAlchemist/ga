import os
import re

filepath = "Common/GA.Domain.Core/Primitives/Note.cs"

with open(filepath, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    # Fix key.Notes.Count
    if "return key.Notes.Count > 0;" in line:
        new_lines.append(line.replace("key.Notes.Count", "keyNotes.Count"))
    elif "key.Notes.Count" in line: # Loose match
        new_lines.append(line.replace("key.Notes.Count", "keyNotes.Count"))
    
    # Fix Intervals.Primitives
    elif "Intervals.Primitives.Accidental" in line:
        new_lines.append(line.replace("Intervals.Primitives.Accidental", "Accidental"))
    elif "Intervals.Accidental" in line: # Just in case
        new_lines.append(line.replace("Intervals.Accidental", "Accidental"))
    
    # Fix imports
    elif line.strip() == "using GA.Domain.Extensions;":
        # Check if we already have primitives extensions
        if not any("using GA.Domain.Primitives.Extensions;" in l for l in lines):
             new_lines.append(line)
             new_lines.append("using GA.Domain.Primitives.Extensions;\n")
        else:
             new_lines.append(line)
    else:
        new_lines.append(line)

with open(filepath, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

