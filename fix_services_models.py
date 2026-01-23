import os

def replace_in_file(filepath, replacements):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        for old, new in replacements:
            new_content = new_content.replace(old, new)
            
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            return True
    except:
        return False
    return False

# Mapping for GA.Domain.Services to point to GA.Domain.Core for models
services_to_core_mappings = [
    ("using GA.Domain.Services.Fretboard.Voicings.Search;", "using GA.Domain.Instruments.Fretboard.Voicings.Search;"),
    ("using GA.Domain.Services.Fretboard.Voicings.Core;", "using GA.Domain.Instruments.Fretboard.Voicings.Core;"),
    ("using GA.Domain.Services.Fretboard.Voicings.Analysis;", "using GA.Domain.Instruments.Fretboard.Voicings.Analysis;"),
    ("using GA.Domain.Services.Fretboard.Analysis;", "using GA.Domain.Instruments.Fretboard.Analysis;"),
    ("using GA.Domain.Services.Fretboard.Biomechanics;", "using GA.Domain.Instruments.Biomechanics;"),
]

def walk_and_fix(root_dir):
    for root, dirs, files in os.walk(root_dir):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                if replace_in_file(filepath, services_to_core_mappings):
                    print(f"Fixed models usage in {filepath}")

walk_and_fix("Common/GA.Domain.Services")
walk_and_fix("Apps")
walk_and_fix("Demos")
walk_and_fix("Tools")
walk_and_fix("GA.Data.MongoDB")
