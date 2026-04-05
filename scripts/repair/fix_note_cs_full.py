import os

filepath = "Common/GA.Domain.Core/Primitives/Note.cs"

with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

# Fix Intervals.Primitives
content = content.replace("Intervals.Primitives.", "GA.Domain.Primitives.")

# Fix key.Notes.Count
# Search for the function TryParse containing key.Notes.Count
if "return key.Notes.Count > 0;" in content:
    content = content.replace("return key.Notes.Count > 0;", "return keyNotes.Count > 0;")
else:
    print("Could not find exact key.Notes.Count line")
    # Fallback: try to find 'key' usage in TryParse
    # It might be 'return key . Notes . Count > 0;' or something weird
    # Or maybe I should just look for the method body in 'KeyNote' record.

# Fix GetIntervalClass and GetInterval visibility
# Check if using GA.Domain.Primitives.Extensions; is present
if "using GA.Domain.Primitives.Extensions;" not in content:
    content = content.replace("using GA.Domain.Extensions;", "using GA.Domain.Extensions;\nusing GA.Domain.Primitives.Extensions;")

# Fix duplicates
content = content.replace("using GA.Domain.Theory.Atonal; // Duplicate", "")
content = content.replace("using GA.Domain.Extensions; // Duplicate", "")

# Fix 'Intervals' standalone if any
content = content.replace(" Intervals.", " GA.Domain.Primitives.")

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)
