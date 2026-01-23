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
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.Primitives", "GA.Domain.Primitives"),
    ("using GA.Business.Core.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
    ("Played.Position", "Position"), # Heuristic
    # Config issue
    ("Config.Get", "Configuration.Get"), # Maybe?
]

replace_in_files("Common/GA.Domain.Services", replacements)
