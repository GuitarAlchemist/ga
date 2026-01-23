import os

def replace_in_files(directory, replacements):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                new_content = content
                for old, new in replacements:
                    new_content = new_content.replace(old, new)
                
                if new_content != content:
                    print(f"Updating {filepath}")
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(new_content)

replacements = [
    ("accidentedContains", "accidentedNotes.Contains"),
    ("accidentedSelect", "accidentedNotes.Select"),
    ("AccidentedToString", "AccidentedNotes.ToString"),
    ("keyRootFirstOrDefault", "keyRootNotes.FirstOrDefault"),
    ("seedScaleCount", "seedScale.Notes.Count"),
    ("seedScaleToImmutableList", "seedScale.Notes.ToImmutableList"),
    ("seedRotate", "seedScale.Notes.Rotate"),
    ("parentScaleCount", "parentScale.Notes.Count"),
    ("parentScaleRotate", "parentScale.Notes.Rotate"),
    ("keyCount", "key.Notes.Count"),
    ("AllInstance", "AllNotes.Instance"),
    ("NotesFirst", "Notes.First"), # If First was Note.First?
    # Pitch.cs specific fixes
    ("public static Pitch CFlat => new(Flat.CFlat);", "public static Pitch CFlat => new(Note.Flat.CFlat);"),
    ("public static Pitch GFlat => new(Flat.GFlat);", "public static Pitch GFlat => new(Note.Flat.GFlat);"),
    ("public static Pitch DFlat => new(Flat.DFlat);", "public static Pitch DFlat => new(Note.Flat.DFlat);"),
    ("public static Pitch AFlat => new(Flat.AFlat);", "public static Pitch AFlat => new(Note.Flat.AFlat);"),
    ("public static Pitch EFlat => new(Flat.EFlat);", "public static Pitch EFlat => new(Note.Flat.EFlat);"),
    ("public static Pitch BFlat => new(Flat.BFlat);", "public static Pitch BFlat => new(Note.Flat.BFlat);"),
    ("public static Pitch FFlat => new(Flat.FFlat);", "public static Pitch FFlat => new(Note.Flat.FFlat);"),
    ("public static Pitch CSharp => new(Sharp.CSharp);", "public static Pitch CSharp => new(Note.Sharp.CSharp);"),
    ("public static Pitch GSharp => new(Sharp.GSharp);", "public static Pitch GSharp => new(Note.Sharp.GSharp);"),
    ("public static Pitch DSharp => new(Sharp.DSharp);", "public static Pitch DSharp => new(Note.Sharp.DSharp);"),
    ("public static Pitch ASharp => new(Sharp.ASharp);", "public static Pitch ASharp => new(Note.Sharp.ASharp);"),
    ("public static Pitch ESharp => new(Sharp.ESharp);", "public static Pitch ESharp => new(Note.Sharp.ESharp);"),
    ("public static Pitch BSharp => new(Sharp.BSharp);", "public static Pitch BSharp => new(Note.Sharp.BSharp);"),
    ("public static Pitch FSharp => new(Sharp.FSharp);", "public static Pitch FSharp => new(Note.Sharp.FSharp);"),
    
    # Generic replacements for Note.Flat / Note.Sharp if they were broken
    ("new(Flat.", "new(Note.Flat."),
    ("new(Sharp.", "new(Note.Sharp."),
    
    ("firstModeSplit", "firstMode.Notes.Split"),
    ("_lazyCharacteristicValue", "_lazyCharacteristicNotes.Value")
]

replace_in_files("Common/GA.Domain.Core", replacements)
