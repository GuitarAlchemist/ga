import os

def replace_in_file(filepath, replacements):
    if not os.path.exists(filepath):
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

# VoicingIndexingService.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/VoicingIndexingService.cs", [
    ("GA.Domain.Primitives.Position", "GA.Domain.Instruments.Primitives.Position"),
    ("GA.Domain.Primitives.RelativeFret", "GA.Domain.Instruments.Primitives.RelativeFret"),
    ("GA.Domain.Primitives.RelativeFretVector", "GA.Domain.Instruments.Primitives.RelativeFretVector"),
])

# VoicingPhysicalAnalyzer.cs
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingPhysicalAnalyzer.cs", [
    ("FretboardGeometry.Calculate", "PhysicalFretboardCalculator.Calculate"),
])

# GpuVectorOps.cs & FretboardPositionMapper.cs (move usings inside namespace)
def move_usings_inside_namespace(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    usings = []
    namespace_line = -1
    code_start = -1
    
    for i, line in enumerate(lines):
        if line.strip().startswith("using "):
            usings.append(line)
        elif line.strip().startswith("namespace "):
            namespace_line = i
        elif namespace_line != -1 and "{" in line: # simplistic check
            code_start = i
            break
            
    # If we found namespace and code start, rewrite
    if namespace_line != -1 and len(usings) > 0:
        new_lines = []
        # Add namespace line
        new_lines.append(lines[namespace_line])
        new_lines.append("\n")
        # Add usings
        new_lines.extend(usings)
        new_lines.append("\n")
        # Add rest of file skipping old usings at top
        for i in range(len(lines)):
            if i == namespace_line: continue
            if lines[i] in usings: continue
            new_lines.append(lines[i])
            
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(new_lines)
            print(f"Moved usings in {filepath}")

move_usings_inside_namespace("Common/GA.Domain.Services/Fretboard/Biomechanics/IK/GpuVectorOps.cs")
move_usings_inside_namespace("Common/GA.Domain.Services/Fretboard/Analysis/FretboardPositionMapper.cs")

# Fix AtonalChordAnalysisService
replace_in_file("Common/GA.Domain.Services/Chords/Analysis/Atonal/AtonalChordAnalysisService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])

# Fix BasicChordExtensionsService
replace_in_file("Common/GA.Domain.Services/Chords/BasicChordExtensionsService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any"),
    ("expectedGA.Domain.Primitives", "expected"),
])

# Fix VoicingHarmonicAnalyzer
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
    ("adjacentGA.Domain.Primitives", "adjacent"),
])

# HybridChordNamingService
replace_in_file("Common/GA.Domain.Services/Chords/HybridChordNamingService.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])

# Fix VoicingKeyFilters
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingKeyFilters.cs", [
    ("VoicingAnalyzer.Get", "Analysis.VoicingAnalyzer.Get"),
])

# Fix GpuVoicingSearchStrategy
replace_in_file("Common/GA.Domain.Services/Fretboard/Voicings/Search/GpuVoicingSearchStrategy.cs", [
    ("using HandSize = GA.Domain.Services.Fretboard.Biomechanics.HandSize;", "using HandSize = GA.Domain.Instruments.Biomechanics.HandSize;"),
])

# Fix ChordTemplateFactory
replace_in_file("Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs", [
    (".GA.Domain.Primitives.Any()", ".Any()"),
])

