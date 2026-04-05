import os

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    new_lines = []
    collected_usings = set()
    
    first_using_index = -1
    last_using_index = -1
    namespace_index = -1
    
    for i, line in enumerate(lines):
        stripped = line.strip()
        indentation = len(line) - len(line.lstrip())
        
        if stripped.startswith("using ") and not stripped.startswith("using var ") and not stripped.startswith("using ("):
            if indentation > 0:
                # Misplaced using
                collected_usings.add(stripped)
                continue
            else:
                if first_using_index == -1: first_using_index = i
                last_using_index = i
        
        if stripped.startswith("namespace ") and indentation > 0:
            # Garbage namespace
            continue
            
        if stripped == "namespace" and indentation > 0:
             # Garbage namespace
            continue

        if stripped.startswith("namespace "):
            if namespace_index == -1: namespace_index = len(new_lines) # Index in new_lines
        
        new_lines.append(line)

    if not collected_usings:
        return # No changes

    # Insert collected usings
    # Find insertion point
    insert_idx = 0
    if last_using_index != -1:
        insert_idx = 0 # We will merge with existing usings logic implicitly by writing to top?
        # Actually, let's just insert at top for simplicity, or after last using.
        # But wait, we removed lines, so indices shifted.
        # Let's just prepend to the file, checking for duplicates is hard without parsing.
        # We will insert after the last top-level using we kept, or at 0.
        pass
    
    # We need to re-find indices in new_lines because we removed lines
    final_lines = []
    inserted = False
    
    # Simple strategy: Insert all collected usings at the very beginning of the file, 
    # but try to avoid duplicates if possible (simple string check).
    # Or better: gather ALL top level usings, add collected, sort/dedup, and rewrite top block.
    # But that's complex.
    
    # Strategy: Insert at top.
    for u in sorted(collected_usings):
        final_lines.append(u + "\n")
        
    final_lines.extend(new_lines)
    
    print(f"Fixed {filepath}")
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(final_lines)

def scan_directory(directory):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                process_file(os.path.join(root, file))

scan_directory("Common/GA.Domain.Services")
