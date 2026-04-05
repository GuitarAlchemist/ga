import os
import re

replacements = [
    ("using GA.Business.Core.Fretboard.Biomechanics;", "using GA.Domain.Instruments.Biomechanics;"),
    ("using GA.Business.Core.Player;", "using GA.Domain.Player;"),
    ("using GA.Business.Core.Tabs;", """using GA.Domain.Repositories;
using GA.Domain.Tabs;"""),
    ("using GA.Business.Core.Fretboard.Voicings.Core;", "using GA.Domain.Instruments.Fretboard.Voicings.Core;"),
    ("using GA.Business.Core.Fretboard.Voicings.Analysis;", "using GA.Domain.Instruments.Fretboard.Voicings.Analysis;"),
    ("using GA.Business.Core.Fretboard.Voicings.Search;", "using GA.Domain.Instruments.Fretboard.Voicings.Search;"),
    ("using GA.Business.Core.Notes.Primitives;", "using GA.Domain.Primitives;"),
]

def update_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_content = content
    
    for old, new in replacements:
        content = content.replace(old, new)
        
    if content != original_content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)

for root, dirs, files in os.walk('.'):
    if "node_modules" in root: continue
    if ".git" in root: continue
    for file in files:
        if file.endswith('.cs'):
            update_file(os.path.join(root, file))