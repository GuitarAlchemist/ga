import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # Mapping table
        mappings = [
            ("GA.Domain.Instruments.Fretboard.Tuning", "GA.Domain.Core.Instruments.Tuning"),
            ("GA.Core.Atonal", "GA.Domain.Core.Theory.Atonal"),
            ("GA.Domain.Core.Intervals", "GA.Domain.Core.Primitives"),
            ("using GA.Domain.Instruments;", "using GA.Domain.Core.Instruments;"),
        ]
        
        for old, new in mappings:
            new_content = new_content.replace(old, new)
            
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run on ML tests
directories = ["Tests/Common/GA.Business.ML.Tests"]
for d in directories:
    for root, dirs, files in os.walk(d):
        for file in files:
            if file.endswith(".cs"):
                fix_file(os.path.join(root, file))