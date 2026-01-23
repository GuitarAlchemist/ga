import os
import re

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Fix corrupted property access
        new_content = re.sub(r'\.GA\.Domain\.(Core\.)?Instruments\.Fretboard\.Voicings\.Analysis\.', '.', new_content)
        new_content = re.sub(r'\.GA\.Domain\.Services\.Fretboard\.Voicings\.Analysis\.', '.', new_content)
        
        # 2. Fix specific broken lines in tests
        new_content = new_content.replace("slashInfo.Notation", "slashInfo.ToString()")
        
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
