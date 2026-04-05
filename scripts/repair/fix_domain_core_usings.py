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
    ("using GA.Domain.Services.Fretboard.Voicings.Core;", "using GA.Domain.Instruments.Fretboard.Voicings.Core;"),
    ("using GA.Domain.Services.Fretboard.Voicings.Search;", "using GA.Domain.Instruments.Fretboard.Voicings.Search;"),
    ("using GA.Domain.Services.Fretboard.Voicings.Analysis;", "using GA.Domain.Instruments.Fretboard.Voicings.Analysis;"),
]

replace_in_files("Common/GA.Domain.Core", replacements)
replace_in_files("Common/GA.Domain.Repositories", replacements)
