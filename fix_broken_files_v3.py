import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Fix missing .Core in Domain usings for Chatbot
        domain_sub_namespaces = ["Instruments", "Theory", "Primitives", "Tabs", "Design", "Unified", "Extensions"]
        for sub in domain_sub_namespaces:
            new_content = new_content.replace(f"using GA.Domain.{sub}", f"using GA.Domain.Core.{sub}")
            new_content = new_content.replace(f"GA.Domain.{sub}", f"GA.Domain.Core.{sub}")
        
        # Cleanup
        new_content = new_content.replace("GA.Domain.Core.Core", "GA.Domain.Core")
        
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run on GaChatbot
for root, dirs, files in os.walk("Apps/GaChatbot"):
    for file in files:
        if file.endswith(".cs"):
            fix_file(os.path.join(root, file))
            
# Fix GaApi/Program.cs again (remove namespace)
prog_path = "Apps/ga-server/GaApi/Program.cs"
if os.path.exists(prog_path):
    with open(prog_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    new_lines = [l for l in lines if not l.strip().startswith("namespace GaApi;")]
    with open(prog_path, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)
    print(f"Removed namespace from {prog_path}")
