import os

def add_using_if_missing(content, using_statement):
    if using_statement.strip() not in content:
        # insert after last using or at top
        lines = content.splitlines()
        last_using_index = -1
        for i, line in enumerate(lines):
            if line.strip().startswith("using "):
                last_using_index = i
        
        if last_using_index != -1:
            lines.insert(last_using_index + 1, using_statement)
        else:
            # Find namespace declaration
            namespace_index = -1
            for i, line in enumerate(lines):
                if line.strip().startswith("namespace "):
                    namespace_index = i
                    break
            
            if namespace_index != -1:
                lines.insert(namespace_index + 1, "")
                lines.insert(namespace_index + 2, using_statement)
            else:
                lines.insert(0, using_statement)
        
        return "\n".join(lines)
    return content

def process_directory(directory):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                original_content = content
                
                # Harmony types
                if "ChordTemplate" in content or "ChordExtension" in content or "ChordQuality" in content or "ChordStackingType" in content or "ChordFormula" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Theory.Harmony;")
                
                # Atonal types
                if "PitchClass" in content or "PitchClassSet" in content or "IntervalClass" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Theory.Atonal;")
                
                # Primitives
                if "Note" in content or "Interval" in content or "Accidental" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Primitives;")
                
                # Fretboard Instruments
                if "Position" in content or "Fret" in content or "Str" in content or "RelativeFret" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Primitives;")
                
                # Positions
                if "PositionLocation" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Positions;")

                # Shapes
                if "FretboardShape" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Shapes;")

                # Tonal
                if "Key" in content or "Scale" in content or "KeySignature" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Theory.Tonal;")
                    content = add_using_if_missing(content, "using GA.Domain.Theory.Tonal.Scales;")

                # Unified
                if "UnifiedModeInstance" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Unified;")

                # Extensions
                if ".ToPitchClassSet" in content or ".ToIntervalStructure" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Extensions;")

                if content != original_content:
                    print(f"Updating {filepath}")
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(content)

process_directory("Common/GA.Domain.Services")
