import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. TabConversion.Api/Program.cs
        if "Tools/GA.TabConversion.Api/Program.cs" in filepath.replace(os.sep, "/"):
            if "namespace GA.TabConversion.Api;" in new_content:
                # Remove the namespace and partial class from Program.cs
                lines = new_content.splitlines()
                clean_lines = []
                for line in lines:
                    if line.strip().startswith("namespace GA.TabConversion.Api;"): continue
                    if line.strip().startswith("using System.Reflection;"): continue
                    if line.strip().startswith("using GA.TabConversion.Api.Services;"): continue
                    if "public partial class Program" in line: continue
                    if line.strip() == "{" or line.strip() == "}": continue
                    clean_lines.append(line)
                new_content = "\n".join(clean_lines)
                
                # Create Program.Partial.cs
                partial_path = os.path.join(os.path.dirname(filepath), "Program.Partial.cs")
                partial_content = "namespace GA.TabConversion.Api;\n\npublic partial class Program { }\n"
                with open(partial_path, 'w', encoding='utf-8') as pf:
                    pf.write(partial_content)
                print(f"Created {partial_path}")

        # 2. Fix MongoDB leftover usings
        new_content = new_content.replace("using Business.Core;", "using GA.Domain.Core;")
        new_content = new_content.replace("using GA.Business.Core;", "using GA.Domain.Core;")

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
