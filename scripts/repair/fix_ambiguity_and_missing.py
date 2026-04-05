import os

def add_using_if_missing(content, using_statement):
    if using_statement.strip() not in content:
        # insert after last using or at top
        lines = content.splitlines()
        last_using_index = -1
        for i, line in enumerate(lines):
            if line.strip().startswith("using ") and not line.strip().startswith("using var"):
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
                # If block scoped, careful. If file scoped, fine.
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
                
                # Biomechanics
                if "HandPose" in content or "FingerType" in content or "HandSize" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Biomechanics;")
                
                # VoicingDocument
                if "VoicingDocument" in content:
                    content = add_using_if_missing(content, "using GA.Domain.Instruments.Fretboard.Voicings.Search;")

                # Disambiguate ChordTemplate
                # If we use ChordTemplate AND we import both Harmony and Tonal.Scales, it is ambiguous.
                # We can alias it.
                if "ChordTemplate" in content and "using GA.Domain.Theory.Tonal.Scales;" in content and "using GA.Domain.Theory.Harmony;" in content:
                     # Add alias at top
                     content = add_using_if_missing(content, "using ChordTemplate = GA.Domain.Theory.Harmony.ChordTemplate;")

                if content != original_content:
                    print(f"Updating {filepath}")
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(content)

process_directory("Common/GA.Domain.Services")
