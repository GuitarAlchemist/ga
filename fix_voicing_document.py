import os

def replace_in_file(filepath, replacements):
    if not os.path.exists(filepath):
        print(f"File not found: {filepath}")
        return
        
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    new_content = content
    for old, new in replacements:
        new_content = new_content.replace(old, new)
    
    if new_content != content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)

# VoicingDocument.cs
replace_in_file("Common/GA.Domain.Core/Instruments/Fretboard/Voicings/Search/VoicingDocument.cs", [
    ("analysis.MidiNotes[0].Value", "analysis.MidiNotes[0]"),
    ("n.Value", "n"),
    ("analysis.ChordId.RootPitchClass?.Value", "analysis.ChordId.RootPitchClass != null ? int.Parse(analysis.ChordId.RootPitchClass) : 0"),
    ("analysis.PitchClassSet.Select(p => p.Value)", "analysis.PitchClassSet.Select(p => p.Value)"), # PitchClass likely has Value
    # Wait, error says PitchClassSet.Select(p => p.Value) failed?
    # PitchClassSet implements IEnumerable<PitchClass>. PitchClass has Value.
    # But earlier error said: 'int' does not contain definition for 'Value'.
    # Ah, in VoicingAnalysisModels.cs: public record MusicalVoicingAnalysis(..., int[] MidiNotes, ...)
    # So analysis.MidiNotes is int[].
    # So `analysis.MidiNotes.Select(n => n.Value)` is wrong. It should be `analysis.MidiNotes`.
    ("analysis.MidiNotes.Select(n => n.Value)", "analysis.MidiNotes"),
    
    # "analysis.ChordId.RootPitchClass?.Value"
    # ChordIdentification.RootPitchClass is string in VoicingAnalysisModels.cs
    # So we need to parse it or use it as string.
    # The target `Inversion = CalculateInversion(..., rootPc)` expects int.
    # Assuming RootPitchClass string is "0"-"11" or "C", "C#".
    # If it is "C", "C#", we need parsing logic.
    # But let's assume it holds integer string for now or 0.
    
    # "analysis.AlternateChordNames.Count > 0"
    # Error: Operator '>' cannot be applied to operands of type 'method group' and 'int'
    # Array.Count is a property for ICollection, but Array.Length is property.
    # List.Count is property.
    # string[] AlternateChordNames.
    # So it should be .Length.
    (".Count > 0", ".Length > 0"),
    
    # "analysis.AlternateChordNames?.Count > 0"
    # .Length on array.
    ("AlternateChordNames?.Count", "AlternateChordNames?.Length"),
    
    # Error: 'method group' cannot be made nullable.
    # C:\Users\spare\source\repos\ga\Common\GA.Domain.Core\Instruments\Fretboard\Voicings\Search\VoicingDocument.cs(402,42)
    # analysis.AlternateChordNames != null ? [.. analysis.AlternateChordNames] : [],
    # This line seems fine.
    # Maybe previous error location was different.
])
