import os

def fix_syntax(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Fix the broken namespace/partial class pattern
    if "namespace " in content and "{" in content:
        lines = content.splitlines()
        clean_lines = []
        in_broken_part = False
        ns = ""
        for line in lines:
            if line.strip().startswith("namespace "):
                ns = line.strip().split(" ")[1].replace(";", "")
                in_broken_part = True
                continue
            if in_broken_part:
                if line.strip() == "{" or line.strip() == "}": continue
                if "public partial class" in line:
                    clean_lines.append(f"namespace {ns};")
                    clean_lines.append("")
                    clean_lines.append(line.strip() + " { }")
                    in_broken_part = False # finished fixing this one
                    continue
            clean_lines.append(line)
        
        new_content = "\n".join(clean_lines)
        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed syntax in {filepath}")

def fix_mappings(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    new_content = content
    # Fix GA.Business.Core -> GA.Domain.Core
    new_content = new_content.replace("GA.Business.Core", "GA.Domain.Core")
    # Fix GA.Core.Atonal -> GA.Domain.Core.Theory.Atonal
    new_content = new_content.replace("using GA.Core.Atonal;", "using GA.Domain.Core.Theory.Atonal;")
    new_content = new_content.replace("using GA.Core.Fretboard;", "using GA.Domain.Core.Instruments.Fretboard;")
    
    # Fix IEmbeddingGenerator missing (it's in GA.Domain.Services.Abstractions)
    if "IEmbeddingGenerator" in content and "using GA.Domain.Services.Abstractions;" not in new_content:
        # insert after first using
        lines = new_content.splitlines()
        for i, line in enumerate(lines):
            if line.strip().startswith("using "):
                lines.insert(i, "using GA.Domain.Services.Abstractions;")
                break
        new_content = "\n".join(lines)

    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Fixed mappings in {filepath}")

# Target files
broken_syntax = [
    "Apps/GaChatbot/Abstractions/IGroundedNarrator.cs",
    "Apps/ga-server/GaApi/Program.cs"
]

for f in broken_syntax:
    fix_syntax(f)

for root, dirs, files in os.walk("."):
    if "obj" in dirs: dirs.remove("obj")
    if "bin" in dirs: dirs.remove("bin")
    for file in files:
        if file.endswith(".cs"):
            fix_mappings(os.path.join(root, file))