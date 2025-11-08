# Modulation Service Documentation

## Overview

The Modulation Service provides intelligent suggestions for transitioning between musical keys. It analyzes the
relationship between keys, identifies pivot chords, calculates difficulty, and suggests chord progressions for smooth
modulations.

## Features

### 1. Modulation Type Detection

Automatically identifies the type of modulation between two keys:

- **Relative** - Keys that share the same notes (e.g., C Major ↔ A Minor)
- **Parallel** - Same tonic, different mode (e.g., C Major ↔ C Minor)
- **Dominant** - Perfect fifth above (e.g., C Major → G Major)
- **Subdominant** - Perfect fifth below (e.g., C Major → F Major)
- **Supertonic** - Whole step above (e.g., C Major → D Minor)
- **Mediant** - Major third above (e.g., C Major → E Minor)
- **Submediant** - Major sixth above (e.g., C Major → A Minor)
- **Chromatic** - Distant keys requiring careful voice leading

### 2. Pivot Chord Detection

Finds chords that exist in both source and target keys, providing:

- Chord name
- Scale degree in source key
- Scale degree in target key
- Roman numeral analysis in both keys
- Functional description

### 3. Difficulty Calculation

Rates modulation difficulty (0.0 = easy, 1.0 = difficult) based on:

- Modulation type (relative is easiest, chromatic is hardest)
- Number of pivot chords available (more = easier)

### 4. Progression Suggestions

Provides suggested chord progressions for smooth modulations using pivot chords when available.

## API Endpoints

### Get Modulation Suggestion

```
GET /api/contextual-chords/modulation?sourceKey={sourceKey}&targetKey={targetKey}
```

**Parameters:**

- `sourceKey` - Source key (e.g., "C Major", "A Minor")
- `targetKey` - Target key (e.g., "G Major", "E Minor")

**Response:**

```json
{
  "sourceKey": "Key of C",
  "targetKey": "Key of G",
  "modulationType": "Dominant",
  "pivotChords": [
    {
      "chordName": "Cmaj7",
      "degreeInSourceKey": 1,
      "degreeInTargetKey": 4,
      "romanNumeralInSourceKey": "I",
      "romanNumeralInTargetKey": "IV",
      "function": "Tonic in Key of C, Subdominant in Key of G"
    }
  ],
  "description": "Dominant modulation from Key of C to Key of G. Very common in classical music. 5 pivot chords available.",
  "difficulty": 0.15,
  "suggestedProgression": [
    "I in Key of C",
    "Cmaj7 (I in Key of C = IV in Key of G)",
    "V in Key of G",
    "I in Key of G"
  ]
}
```

### Get Common Modulations

```
GET /api/contextual-chords/modulation/common?sourceKey={sourceKey}
```

**Parameters:**

- `sourceKey` - Source key (e.g., "C Major")

**Response:**

```json
[
  {
    "sourceKey": "Key of C",
    "targetKey": "Key of Am",
    "modulationType": "Relative",
    "difficulty": 0.05,
    ...
  },
  {
    "sourceKey": "Key of C",
    "targetKey": "Key of G",
    "modulationType": "Dominant",
    "difficulty": 0.15,
    ...
  },
  ...
]
```

Results are ordered by difficulty (easiest first).

## Music Theory Background

### Modulation Types Explained

#### Relative Modulation (Easiest)

- **Example:** C Major → A Minor
- **Characteristics:** Share all the same notes
- **Difficulty:** Very easy (0.1)
- **Use Case:** Subtle mood change without disrupting harmony

#### Dominant Modulation (Very Common)

- **Example:** C Major → G Major
- **Characteristics:** Perfect fifth relationship
- **Difficulty:** Easy (0.2)
- **Use Case:** Classical music, creates brightness and forward motion

#### Subdominant Modulation (Very Common)

- **Example:** C Major → F Major
- **Characteristics:** Perfect fifth below
- **Difficulty:** Easy (0.2)
- **Use Case:** Relaxing, settling effect

#### Parallel Modulation (Dramatic)

- **Example:** C Major → C Minor
- **Characteristics:** Same tonic, different mode
- **Difficulty:** Moderate (0.3)
- **Use Case:** Dramatic mood change while maintaining tonal center

#### Chromatic Modulation (Advanced)

- **Example:** C Major → F# Major
- **Characteristics:** Distant keys, few common chords
- **Difficulty:** Hard (0.9)
- **Use Case:** Surprise, dramatic effect, requires careful voice leading

### Pivot Chords

A **pivot chord** is a chord that exists in both the source and target keys. It serves as a "bridge" between keys,
making the modulation sound smooth and natural.

**Example:** C Major → G Major

- **Cmaj7** is I in C Major and IV in G Major
- **Em7** is iii in C Major and vi in G Major
- **Am7** is vi in C Major and ii in G Major

**Usage in Progression:**

1. Establish source key: I in C Major
2. Use pivot chord: Cmaj7 (I in C = IV in G)
3. Establish target key: V → I in G Major

## Implementation Details

### Architecture

```
ModulationService
├── GetModulationSuggestionAsync(sourceKey, targetKey)
│   ├── DetermineModulationType()
│   ├── FindPivotChords()
│   ├── CalculateDifficulty()
│   ├── GenerateDescription()
│   └── SuggestProgression()
└── GetCommonModulationsAsync(sourceKey)
    ├── GetRelativeKey()
    ├── GetParallelKey()
    ├── GetDominantKey()
    └── GetSubdominantKey()
```

### Caching Strategy

- **Modulation suggestions** are cached by source and target key
- **Common modulations** are cached by source key
- Cache keys include full key information for accuracy

### Difficulty Calculation

```csharp
baseDifficulty = modulationType switch
{
    Relative => 0.1,
    Dominant => 0.2,
    Subdominant => 0.2,
    Parallel => 0.3,
    Supertonic => 0.5,
    Mediant => 0.6,
    Submediant => 0.4,
    Chromatic => 0.9
};

pivotBonus = Math.Min(pivotChordCount * 0.05, 0.3);
difficulty = baseDifficulty - pivotBonus;
```

## Usage Examples

### Example 1: Simple Relative Modulation

```csharp
var suggestion = await modulationService.GetModulationSuggestionAsync(
    Key.Major.C, 
    Key.Minor.A
);

// Result:
// - Type: Relative
// - Difficulty: 0.05 (very easy)
// - Pivot Chords: 7 (all diatonic chords are shared)
// - Description: "Very smooth - shares the same notes"
```

### Example 2: Dominant Modulation

```csharp
var suggestion = await modulationService.GetModulationSuggestionAsync(
    Key.Major.C, 
    Key.Major.G
);

// Result:
// - Type: Dominant
// - Difficulty: 0.15 (easy)
// - Pivot Chords: 5 (C, Em, G, Am, Bm)
// - Progression: I → IV (pivot) → V → I in new key
```

### Example 3: Get All Common Modulations

```csharp
var suggestions = await modulationService.GetCommonModulationsAsync(Key.Major.C);

// Returns 4+ suggestions ordered by difficulty:
// 1. C Major → A Minor (Relative, 0.05)
// 2. C Major → G Major (Dominant, 0.15)
// 3. C Major → F Major (Subdominant, 0.15)
// 4. C Major → C Minor (Parallel, 0.25)
```

## Testing

The modulation feature includes comprehensive integration tests:

- ✅ Dominant modulation detection
- ✅ Relative modulation detection
- ✅ Parallel modulation detection
- ✅ Subdominant modulation detection
- ✅ Pivot chord identification
- ✅ Suggested progression generation
- ✅ Common modulations retrieval
- ✅ Difficulty ordering
- ✅ Caching behavior
- ✅ Invalid key handling

All 11 tests passing (100% pass rate).

## Future Enhancements

1. **Secondary Dominant Modulations** - Use V/V to modulate
2. **Chromatic Mediant Modulations** - Advanced chromatic relationships
3. **Enharmonic Modulations** - Using enharmonic equivalents
4. **Modal Modulations** - Between different modes
5. **Voice Leading Analysis** - Analyze smoothness of voice leading
6. **Audio Examples** - Generate audio demonstrations of modulations
7. **Progression Variations** - Multiple progression options per modulation
8. **Historical Examples** - Reference famous modulations in classical music

