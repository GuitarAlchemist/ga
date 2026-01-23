import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        if "ToPitchClassSet" in content and "using GA.Domain.Core.Extensions;" not in new_content:
            new_content = "using GA.Domain.Core.Extensions;\n" + new_content
            
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except:
        pass

# Run on Domain.Core
for root, dirs, files in os.walk("Common/GA.Domain.Core"):
    for file in files:
        if file.endswith(".cs"):
            fix_file(os.path.join(root, file))
