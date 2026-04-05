import os

def clean_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    new_lines = []
    
    # Logic: Keep initial block of comments/usings. Once we hit namespace or something else, stop allowing 'using GA.'
    # Actually, just allow 'using' until we see 'namespace' or 'public'.
    # But wait, namespace declaration can be followed by usings.
    # Safe heuristic: A 'using' line is valid if it is NOT preceded by lines that are NOT usings/comments/namespace.
    # But 'using var' IS a using line string-wise.
    # Distinguish 'using ' (directive) vs 'using var' or 'using('.
    
    # Better: Scan file. Identify "header".
    # Remove 'using GA...' if it appears after the header.
    # But how to define header?
    # If I just remove any line that is `using GA...` AND is NOT indented, but appears inside a method?
    # If I inserted it, it is NOT indented.
    # But code inside method is indented.
    # So `using GA...` unindented line inside a method looks like:
    #    using var x = ...;
    # using GA...;
    #    next line;
    #
    # So if I find a `using GA...` line that is NOT indented (or has different indentation?)
    # Let's just remove ALL `using GA...` lines that are NOT in the first 50 lines?
    # No, files can be long.
    
    # Let's use the fact that I inserted them after `using var`.
    # So if previous line contains `using var` or `using (`, and current line is `using GA...`, remove it.
    
    for i, line in enumerate(lines):
        stripped = line.strip()
        
        # Remove standalone 'namespace'
        if stripped == "namespace":
            continue
            
        # Check if it is a 'using directive' (not statement)
        if stripped.startswith("using ") and not stripped.startswith("using var ") and not stripped.startswith("using ("):
            # It is a directive.
            # Is it valid?
            # If it is unindented (start of line)
            # Check context.
            # If previous line was indented?
            if i > 0:
                prev_line = lines[i-1]
                if len(prev_line) - len(prev_line.lstrip()) > 0:
                    # Previous line was indented. Current line is `using ...`.
                    # If current line is NOT indented (length == lstrip), it breaks flow but might be valid?
                    # But if I inserted it, I inserted it without indentation?
                    # My script `lines.insert` inserts string. If string has no spaces, it has no indentation.
                    # So yes, unindented `using` after indented line is garbage.
                    if len(line) - len(line.lstrip()) == 0:
                        continue
            
            # Also check if it follows `using var`
            if i > 0 and "using var" in lines[i-1]:
                continue
            if i > 0 and "using (" in lines[i-1]:
                continue
                
        new_lines.append(line)
        
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

files = [
    "Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs",
    "Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingCacheSerialization.cs",
    "Common/GA.Domain.Services/Fretboard/Biomechanics/IK/LerobotWristPriorLoader.cs",
    "Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs"
]

for f in files:
    if os.path.exists(f):
        clean_file(f)
        print(f"Cleaned {f}")
