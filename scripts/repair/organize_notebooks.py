import os
import json

ROOT_DIR = r"C:\Users\spare\source\repos\ga\Notebooks"

TAG_RULES = [
    (lambda p, n: "MusicTheory" in p, ["theory", "music"]),
    (lambda p, n: "Experiments" in p, ["experiment"]),
    (lambda p, n: "Samples" in p, ["sample"]),
    (lambda p, n: "Visualizer" in n, ["viz", "demo"]),
    (lambda p, n: "F Sharp" in n, ["language", "fsharp"]),
    (lambda p, n: "C Sharp" in n, ["language", "csharp"]),
    (lambda p, n: "Intro" in n, ["tutorial"]),
    (lambda p, n: "Scale" in n, ["scales"]),
    (lambda p, n: "Chord" in n or "Voicing" in n, ["chords"]),
]

def organize():
    count = 0
    print(f"Scanning {ROOT_DIR}...")
    for root, dirs, files in os.walk(ROOT_DIR):
        for file in files:
            if file.endswith(".ipynb"):
                path = os.path.join(root, file)
                try:
                    with open(path, "r", encoding="utf-8") as f:
                        data = json.load(f)
                    
                    # Ensure metadata exists
                    if "metadata" not in data:
                        data["metadata"] = {}
                    
                    current_tags = set(data["metadata"].get("tags", []))
                    
                    # Apply rules
                    rel_path = os.path.relpath(root, ROOT_DIR)
                    name = file
                    
                    for condition, tags in TAG_RULES:
                        if condition(rel_path, name):
                            current_tags.update(tags)
                    
                    # Convert back to list and save if changed
                    new_tags = sorted(list(current_tags))
                    
                    if new_tags != sorted(data["metadata"].get("tags", [])):
                        data["metadata"]["tags"] = new_tags
                        with open(path, "w", encoding="utf-8") as f:
                            json.dump(data, f, indent=1)
                        print(f"Updated {file}: {new_tags}")
                        count += 1
                    else:
                        print(f"Skipped {file} (no changes)")
                        
                except Exception as e:
                    print(f"Error processing {file}: {e}")

    print(f"Done. Updated {count} notebooks.")

if __name__ == "__main__":
    organize()
