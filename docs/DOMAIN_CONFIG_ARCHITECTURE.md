# Domain and Configuration Layer Architecture

**Date**: January 2026  
**Status**: ✅ Architecture Pattern Documentation

## Overview

This document explains the relationship between the pure domain layer (`GA.Domain.Core`) and the configuration layer (`GA.Business.Config`), and why they are intentionally separated with a one-way dependency.

## Layer Architecture

```
┌─────────────────────────────────────────────┐
│ Layer 1.5: GA.Domain.Core                   │
│ Pure Mathematical Domain Models             │
│                                             │
│ • PitchClassSet (all 4096 possible sets)   │
│ • Chord, Scale, Note, Interval             │
│ • Fretboard, Tuning                        │
│ • Generated algorithmically                 │
│ • Zero external dependencies               │
│ • Portable to any environment              │
└─────────────────────────────────────────────┘
                    ▲
                    │
                    │ depends on
                    │
┌─────────────────────────────────────────────┐
│ Layer 2: GA.Business.Config                │
│ Named Instances & Cultural Metadata        │
│                                             │
│ • Scales.yaml → Named scale definitions    │
│ • IconicChords.yaml → Famous voicings      │
│ • ChordProgressions.yaml → Common patterns │
│ • Instruments.yaml → Specific tunings      │
│ • Loads YAML → Creates domain objects      │
└─────────────────────────────────────────────┘
```

## The Dependency Rule

**GA.Business.Config DEPENDS ON GA.Domain.Core** ✅

- Config layer **uses** domain classes to create instances
- Domain layer **never references** config files
- This is a one-way dependency: Config → Domain

**GA.Domain.Core is INDEPENDENT** ✅

- No knowledge of YAML files
- No knowledge of configuration system
- Can be used in any context (Unity, Blazor WASM, Mobile, etc.)

## What Each Layer Provides

### GA.Domain.Core: "What CAN Exist"

The domain layer defines the **mathematical space** of all possible musical objects:

- **All 4096 pitch class sets** (computed algorithmically)
- **All possible chords** (any root + any formula)
- **All possible scales** (any collection of notes)
- **Domain rules** via `[DomainInvariant]` attributes
- **Relationships** via `[DomainRelationship]` attributes

**Example**: The `PitchClassSet` class can represent any subset of the 12-tone chromatic scale.

### GA.Business.Config: "What IS Culturally Significant"

The config layer provides **named instances and cultural context**:

- **~100 named scales** (Major, Dorian, Persian, etc.) from all 4096 possible
- **Famous chord voicings** (Hendrix Chord, James Bond Chord)
- **Common progressions** (I-IV-V, ii-V-I)
- **Cultural metadata** (Artist, Era, Genre, Usage notes)

**Example**: `Scales.yaml` defines that pitch class set `{0,2,4,5,7,9,11}` is called "Major" or "Ionian" and is "fundamental to Western harmony."

## How They Interact

### 1. Config Loads YAML

```fsharp
// ScalesConfig.fs
let loadScalesData () =
    let yaml = File.ReadAllText("Scales.yaml")
    let data = deserializer.Deserialize<ScalesYaml>(yaml)
    scalesData <- Some data
```

### 2. Config Creates Domain Objects

```fsharp
// Config parses notes and creates domain Scale instances
let notes = parseNotes "C D E F G A B"  // From YAML
let majorScale = new Scale(notes)       // Domain object
```

### 3. Config Adds Metadata

```yaml
# IconicChords.yaml
- Name: "Hendrix Chord"
  TheoreticalName: "E7#9"
  Artist: "Jimi Hendrix"
  Song: "Purple Haze"
  Era: "1960s"
  PitchClasses: [4, 8, 11, 2, 7]  # Creates PitchClassSet
```

### 4. Applications Use Both

```csharp
// Application layer
using GA.Domain.Core.Theory.Tonal.Scales;
using GA.Business.Config;

// Pure domain usage (no config needed)
var allPitchClassSets = PitchClassSet.Items;  // All 4096

// Config-enhanced usage
var namedScales = ScalesConfig.GetAllScales();  // ~100 with names
var majorScale = namedScales.First(s => s.Name == "Major");
```

## Benefits of This Architecture

### 1. **Portability**
The domain layer can be used in any environment without requiring file I/O or YAML parsers:
- Unity game engine
- Blazor WebAssembly
- Mobile apps
- Embedded systems

### 2. **Testability**
Domain logic can be tested without configuration files:
```csharp
[Fact]
public void PitchClassSet_NormalForm_IsComputed()
{
    var set = new PitchClassSet(new[] { 0, 4, 7 }); // C major triad
    Assert.True(set.IsNormalForm);
    // No config files needed!
}
```

### 3. **Flexibility**
Different applications can provide different configurations:
- Educational app: Focus on common scales
- Research tool: All 4096 pitch class sets
- Game: Custom fantasy scales

### 4. **Separation of Concerns**
- **Domain**: Mathematical correctness, invariants, relationships
- **Config**: Cultural context, naming, practical usage

## Anti-Patterns to Avoid

### ❌ Domain Depending on Config

```csharp
// WRONG: Domain class referencing config
public class Scale
{
    public string Name => ScalesConfig.GetNameFor(this); // ❌ BAD
}
```

### ✅ Config Depending on Domain

```csharp
// CORRECT: Config providing metadata for domain
public class ScaleInfo
{
    public Scale DomainScale { get; set; }  // ✅ GOOD
    public string Name { get; set; }
    public string Usage { get; set; }
}
```

## Configuration File Examples

### Scales.yaml
Defines named scales with cultural context:
```yaml
Major:
  Notes: "C D E F G A B"
  AlternateNames: ["Ionian"]
  Category: "Western"
  Usage: "Fundamental to Western harmony"
  RelatedScales: ["NaturalMinor"]
```

### IconicChords.yaml
Defines famous chord voicings with historical context:
```yaml
- Name: "Hendrix Chord"
  TheoreticalName: "E7#9"
  Artist: "Jimi Hendrix"
  Song: "Purple Haze"
  PitchClasses: [4, 8, 11, 2, 7]
  GuitarVoicing: [0, 7, 6, 7, 8, 0]
```

### ChordProgressions.yaml
Defines common chord sequences:
```yaml
- Name: "I-IV-V"
  Description: "The most common progression in Western music"
  Genre: "Rock/Pop/Blues"
  Steps: ["I", "IV", "V"]
```

## Schema Generation

Both layers contribute to the domain schema:

**From GA.Domain.Core**:
- Entity definitions (classes)
- Invariants (`[DomainInvariant]`)
- Relationships (`[DomainRelationship]`)
- Computed properties

**From GA.Business.Config**:
- Named instances
- Cultural metadata
- Usage examples
- Semantic labels

The schema generation tool can read both to produce a complete picture:
1. **Structure** from domain classes
2. **Examples** from configuration

## Summary

The separation between `GA.Domain.Core` and `GA.Business.Config` follows the **Dependency Inversion Principle**:

- **Domain is pure** → No dependencies, fully testable, portable
- **Config depends on domain** → Provides named instances and context
- **One-way dependency** → Config → Domain (never the reverse)

This architecture ensures that the mathematical foundation (domain) remains clean and reusable, while the practical, cultural layer (config) adds human-meaningful labels and context without polluting the core abstractions.

## Related Documents

- [DOMAIN_ARCHITECTURE_REVIEW.md](DOMAIN_ARCHITECTURE_REVIEW.md) - Comprehensive domain model review
- [GA.Business.Config/README.md](../Common/GA.Business.Config/README.md) - Configuration layer overview
- [GA.Domain.Core/README.md](../Common/GA.Domain.Core/README.md) - Pure domain layer overview
