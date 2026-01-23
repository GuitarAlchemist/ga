import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Add missing usings
        if "ToPitchClassSet" in content and "using GA.Domain.Core.Extensions;" not in new_content:
             new_content = "using GA.Domain.Core.Extensions;\n" + new_content
        
        # 2. Fix corrupted property access
        new_content = new_content.replace(".GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis.", ".")
        
        # 3. Add missing usings for Notation extension
        if ".Notation" in content and "using GA.Domain.Core.Primitives.Extensions;" not in new_content:
             new_content = "using GA.Domain.Core.Primitives.Extensions;\n" + new_content

        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run on GA.Business.Core.Tests
directories = ["Tests/Common/GA.Business.Core.Tests"]
for d in directories:
    for root, dirs, files in os.walk(d):
        for file in files:
            if file.endswith(".cs"):
                fix_file(os.path.join(root, file))