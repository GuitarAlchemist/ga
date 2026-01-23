import os

def fix_using_style(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    usings = []
    namespace_line_idx = -1
    other_lines = []
    
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("using ") and not stripped.startswith("using var") and not stripped.startswith("using ("):
            usings.append(line)
        elif stripped.startswith("namespace ") and namespace_line_idx == -1:
            namespace_line_idx = i
        else:
            other_lines.append(line)
            
    if namespace_line_idx != -1 and usings:
        new_content = []
        # Add namespace
        new_content.append(lines[namespace_line_idx])
        new_content.append("\n")
        # Add usings
        for u in usings:
            new_content.append(u)
        new_content.append("\n")
        # Add rest
        # We need to filter out the namespace line from other_lines
        final_other_lines = []
        for line in other_lines:
            if line.strip().startswith("namespace "): continue
            final_other_lines.append(line)
        
        new_content.extend(final_other_lines)
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(new_content)
        print(f"Fixed style in {filepath}")

# For now just target the ones in Core that failed
files = [
    "Common/GA.Domain.Core/Instruments/Fretboard/Voicings/Search/VoicingDocument.cs",
    "Common/GA.Domain.Core/Instruments/Shapes/FretboardShape.cs",
    "Common/GA.Domain.Core/Instruments/Shapes/IShapeGraphBuilder.cs",
    "Common/GA.Domain.Core/Instruments/Shapes/ShapeGraph.cs",
    "Common/GA.Domain.Core/Instruments/Shapes/ShapeGraphBuilder.cs",
    "Common/GA.Domain.Core/Instruments/Tuning.cs"
]

for f in files:
    fix_using_style(f)
