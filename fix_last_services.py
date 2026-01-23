import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Syntax fix for TabConversion.Api/Program.cs
        if "Tools/GA.TabConversion.Api/Program.cs" in filepath.replace(os.sep, "/"):
            if "namespace GA.TabConversion.Api" in new_content:
                lines = new_content.splitlines()
                clean_lines = []
                in_broken_part = False
                for line in lines:
                    if line.strip().startswith("namespace GA.TabConversion.Api"):
                        in_broken_part = True
                        continue
                    if in_broken_part:
                        continue
                    clean_lines.append(line)
                
                clean_lines.append("namespace GA.TabConversion.Api;")
                clean_lines.append("")
                clean_lines.append("using System.Reflection;")
                clean_lines.append("using GA.TabConversion.Api.Services;")
                clean_lines.append("")
                clean_lines.append("public partial class Program { }")
                new_content = "\n".join(clean_lines)

        # 2. Fix missing .Core in Domain usings
        domain_sub_namespaces = ["Instruments", "Theory", "Primitives", "Tabs", "Design", "Unified", "Extensions"]
        for sub in domain_sub_namespaces:
            new_content = new_content.replace(f"using GA.Domain.{sub}", f"using GA.Domain.Core.{sub}")
            new_content = new_content.replace(f"using Domain.Core.{sub}", f"using GA.Domain.Core.{sub}")
        
        # Cleanup double .Core.Core
        new_content = new_content.replace("GA.Domain.Core.Core", "GA.Domain.Core")
        
        # 3. Special case for GA.Business.Core leftover in MongoDB
        new_content = new_content.replace("using GA.Business.Core.", "using GA.Domain.Core.")

        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run
for root, dirs, files in os.walk("."):
    if "obj" in dirs: dirs.remove("obj")
    if "bin" in dirs: dirs.remove("bin")
    if ".git" in dirs: dirs.remove(".git")
    for file in files:
        if file.endswith(".cs"):
            fix_file(os.path.join(root, file))
