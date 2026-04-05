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
    ("using GA.Business.Core.Fretboard", "using GA.Domain.Instruments.Fretboard"),
    ("using GA.Business.Core.Fretboard.Primitives", "using GA.Domain.Instruments.Primitives"),
    ("using GA.Domain.Theory.Atonal.Primitives", "using GA.Domain.Theory.Atonal"),
    ("using Config;", "using GA.Business.Config;"),
    ("using GA.Business.Core.Chords", "using GA.Domain.Services.Chords"),
    ("using GA.Domain.Primitives.Position", "using GA.Domain.Instruments.Primitives.Position"), # If FQN usage
    ("using Position = GA.Domain.Primitives.Position", "using Position = GA.Domain.Instruments.Primitives.Position"),
]

# Add specific using if Position is used but not imported
def add_using_if_missing(content, using_statement):
    if using_statement.strip() not in content:
        # insert after last using or at top
        lines = content.splitlines()
        last_using_index = -1
        for i, line in enumerate(lines):
            if line.strip().startswith("using ") and not line.strip().startswith("using var"):
                last_using_index = i
        
        if last_using_index != -1:
            lines.insert(last_using_index + 1, using_statement)
        else:
            lines.insert(0, using_statement)
        return "\n".join(lines)
    return content

def process_directory(directory):
    replace_in_files(directory, replacements)
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                original_content = content
                if "Position" in content and "GA.Domain.Instruments.Primitives" not in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Primitives;")
                
                if "Played" in content and "GA.Domain.Instruments.Primitives" not in content: # Played might be in Primitives or Fretboard
                     pass # Played is likely a variable name or method, ignore unless it's a type

                if content != original_content:
                    print(f"Adding usings to {filepath}")
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(content)

process_directory("Common/GA.Domain.Services")
