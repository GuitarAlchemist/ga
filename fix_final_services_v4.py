import os

def fix_file(filepath):
    try:
        with open(filepath, 'rb') as f:
            raw = f.read()
        
        # Remove BOM if present
        if raw.startswith(b'\xef\xbb\xbf'):
            raw = raw[3:]
            
        content = raw.decode('utf-8')
        
        if "namespace " in content and "using " in content:
            lines = content.splitlines()
            namespace_line = ""
            usings = []
            others = []
            
            for line in lines:
                if line.strip().startswith("namespace "):
                    namespace_line = line
                elif line.strip().startswith("using ") and not line.strip().startswith("using (") and not line.strip().startswith("using var"):
                    usings.append(line)
                elif line.strip():
                    others.append(line)
            
            if namespace_line and usings:
                new_content = [namespace_line, ""]
                new_content.extend(usings)
                new_content.append("")
                new_content.extend(others)
                
                final_content = "\n".join(new_content)
                if final_content != content:
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(final_content)
                    print(f"Fixed {filepath}")
    except:
        pass

# Run on GA.Business.Core.Tests
for root, dirs, files in os.walk("Tests/Common/GA.Business.Core.Tests"):
    for file in files:
        if file.endswith(".cs"):
            fix_file(os.path.join(root, file))

