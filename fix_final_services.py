import os

def fix_usings_and_style(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Fix missing GA. prefix in usings
        new_content = new_content.replace("using Domain.Core.", "using GA.Domain.Core.")
        new_content = new_content.replace("using Business.Core.", "using GA.Domain.Core.")
        
        # 2. Fix IDE0065 (Usings MUST be INSIDE namespace)
        # Only do this if we see usings OUTSIDE the namespace
        lines = new_content.splitlines()
        namespace_idx = -1
        first_using_idx = -1
        
        for i, line in enumerate(lines):
            stripped = line.strip()
            if stripped.startswith("namespace ") and namespace_idx == -1:
                namespace_idx = i
            if stripped.startswith("using ") and not stripped.startswith("using (") and not stripped.startswith("using var") and first_using_idx == -1:
                first_using_idx = i
        
        if namespace_idx != -1 and first_using_idx != -1 and first_using_idx < namespace_idx:
            # We have usings before namespace. Move them inside.
            usings = []
            non_usings_before_namespace = []
            after_namespace = []
            
            for i, line in enumerate(lines):
                stripped = line.strip()
                if i < namespace_idx:
                    if stripped.startswith("using ") and not stripped.startswith("using (") and not stripped.startswith("using var"):
                        usings.append(line)
                    else:
                        non_usings_before_namespace.append(line)
                elif i == namespace_idx:
                    pass # handled separately
                else:
                    after_namespace.append(line)
            
            result = []
            result.extend(non_usings_before_namespace)
            result.append(lines[namespace_idx])
            result.append("")
            result.extend(usings)
            # Add a newline if there wasn't one
            if after_namespace and after_namespace[0].strip() != "":
                result.append("")
            result.extend(after_namespace)
            new_content = "\n".join(result)

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
            fix_usings_and_style(os.path.join(root, file))