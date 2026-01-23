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

# VoicingAnalyzer.cs
# Match record definition:
# VoicingCharacteristics, PhysicalLayout, PlayabilityInfo, PerceptualQualities, ChordId, MidiNotes, EquivalenceInfo, ToneInventory, AlternateChordNames, ModeInfo, IntervallicInfo, SemanticTags, PitchClassSet
# 13 arguments
# My call has too many or wrong order.
# Error messages indicate argument type mismatches starting at arg 1.
# Arg 1 is midiNotes (int[]), but expected VoicingCharacteristics.
# Arg 2 is pitchClassSet, but expected PhysicalLayout.
# It seems I completely reordered arguments in my previous replacement script but didn't match the new record definition.
# I need to reorder them to match:
# (curVoiceChars, physicalLayout, playabilityInfo, perceptualQualities, chordId, midiNotes, equivalenceInfo, toneInventory, alternateChordNames, modeInfo, intervallicInfo, semanticTags, pitchClassSet)

replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingAnalyzer.cs", [
    # Replace the whole constructor call block
    ("return new(\n            // Core\n            midiNotes,\n            pitchClassSet,\n\n            // Layer 1: Identity\n            chordId,\n            alternateChordNames,\n            modeInfo,\n            equivalenceInfo,\n\n            // Layer 2: Sound\n            curVoiceChars,\n            toneInventory,\n            perceptualQualities,\n            symmetricalInfo,\n            intervallicInfo,\n            chromaticNotes,\n\n            // Layer 3: Hands\n            physicalLayout,\n            playabilityInfo,\n            ergonomicsInfo,\n\n            // Contextual\n            semanticTags\n            \n        );", 
     "return new(\n            curVoiceChars,\n            physicalLayout,\n            playabilityInfo,\n            perceptualQualities,\n            chordId,\n            midiNotes.Select(n => n.Value).ToArray(),\n            equivalenceInfo,\n            toneInventory,\n            alternateChordNames,\n            modeInfo,\n            intervallicInfo,\n            semanticTags.ToArray(),\n            pitchClassSet\n        );"),
])

# HybridChordNamingService.cs
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    (".Count", ""), # Assuming it was .Count on a scalar property? Or checking intervals count?
    # Error: 'ChordTemplate' does not contain definition for 'Count'
    # Maybe it was 'Intervals.Count'?
    # Context: if (template.Count ...) -> if (template.Intervals.Count ...)
    ("template.Count", "template.Intervals.Count"),
])

