# Contextual Chord Analysis - Implementation Summary

## 📋 Overview

This document summarizes the implementation of Phase 1 of the contextual chord analysis backend services for the Guitar Alchemist project. The goal is to provide proper musical and physical contextualization for chords and scales, addressing issues from the legacy Delphi system that generated too many chords with incorrect naming and too many voicings due to lack of context.

---

## ✅ What Has Been Implemented

### 1. **Core Models** (`Apps/ga-server/GaApi/Models/ContextualChordModels.cs`)

#### ChordInContext
A chord analyzed within a specific musical context (key, scale, or mode):
- **Template**: The chord template
- **Root**: The root pitch class
- **ContextualName**: Context-aware name (e.g., "Cmaj7" in C major)
- **ScaleDegree**: Scale degree (1-7 for diatonic, null for chromatic)
- **Function**: Harmonic function (Tonic, Subdominant, Dominant, etc.)
- **Commonality**: Probability score (0.0-1.0)
- **IsNaturallyOccurring**: True for diatonic chords
- **RomanNumeral**: Roman numeral notation (e.g., "Imaj7", "V/V")
- **FunctionalDescription**: Detailed functional description
- **SecondaryDominant**: Information about secondary dominants (NEW)
- **Modulation**: Modulation possibilities (NEW)

#### SecondaryDominantInfo (NEW)
Information about secondary dominant chords:
- **TargetDegree**: Target scale degree (e.g., 5 for V/V)
- **TargetChordName**: Target chord name (e.g., "G" for V/V in C major)
- **Notation**: Roman numeral notation (e.g., "V/V", "V7/ii")
- **Description**: Descriptive text
- **IsPartOfTwoFive**: True if part of a ii-V progression

#### ModulationInfo (NEW)
Information about modulation possibilities:
- **TargetKey**: Target key for modulation
- **Type**: Modulation type (Relative, Parallel, Dominant, etc.)
- **IsPivotChord**: True if chord exists in both keys
- **RomanNumeralInOriginalKey**: Roman numeral in original key
- **RomanNumeralInTargetKey**: Roman numeral in target key
- **Description**: Modulation description

#### ChordFilters
Filtering options for chord queries:
- **Extension**: Maximum chord extension (Triad, Seventh, Ninth, etc.)
- **StackingType**: Stacking type (Tertian, Quartal, Quintal, etc.)
- **OnlyNaturallyOccurring**: Include only diatonic chords
- **IncludeBorrowedChords**: Include modal interchange chords (NEW)
- **IncludeSecondaryDominants**: Include V/x, V7/x chords (NEW)
- **IncludeSecondaryTwoFive**: Include ii-V progressions (NEW)
- **IncludeModulations**: Include modulation suggestions (NEW)
- **MinCommonality**: Minimum commonality threshold
- **Limit**: Maximum number of results

---

### 2. **Services** (`Apps/ga-server/GaApi/Services/`)

#### ContextualChordService
Main service for contextual chord analysis:

**Public Methods:**
- `GetChordsForKeyAsync(Key key, ChordFilters filters)` - Get chords for a key
- `GetChordsForScaleAsync(ScaleMode scale, ChordFilters filters)` - Get chords for a scale
- `GetChordsForModeAsync(ScaleMode mode, ChordFilters filters)` - Get chords for a mode

**Private Methods:**
- `GenerateDiatonicChords()` - Generate naturally occurring chords
- `GetCommonBorrowedChords()` - Generate borrowed chords (modal interchange)
- `GetSecondaryDominants()` - Generate secondary dominant chords (NEW)
- `GetSecondaryTwoFiveChords()` - Generate secondary ii-V progressions (NEW)
- `ApplyFilters()` - Apply filtering logic
- `RankByCommonality()` - Rank chords by probability

**Features:**
- ✅ Diatonic chord generation (7 chords per key)
- ✅ Borrowed chords from parallel keys (modal interchange)
- ✅ Secondary dominants (V/ii, V/iii, V/IV, V/V, V/vi) (NEW)
- ✅ Secondary ii-V progressions (ii/IV-V/IV, ii/V-V/V, etc.) (NEW)
- ✅ Proper Roman numeral notation
- ✅ Functional analysis (Tonic, Subdominant, Dominant)
- ✅ Commonality ranking
- ✅ Flexible filtering options
- ✅ Caching for performance

#### VoicingFilterService
Service for voicing filtering and ranking:

**Public Methods:**
- `GetVoicingsForChordAsync(ChordTemplate template, PitchClass root, VoicingFilters filters)` - Get voicings for a chord

**Features:**
- ✅ Voicing generation using `FretboardChordsGenerator`
- ✅ Physical analysis (fret span, finger stretch, barre requirements)
- ✅ Psychoacoustic analysis (consonance, brightness, tension)
- ✅ Filtering by difficulty, fret range, open strings, barres, etc.
- ✅ Ranking by utility score
- ✅ Style tag assignment (Jazz, Rock, Classical, etc.)
- ✅ CAGED shape detection

---

### 3. **API Endpoints** (`Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs`)

- `GET /api/contextual-chords/keys/{keyName}` - Get chords for a key
- `GET /api/contextual-chords/scales/{scaleName}` - Get chords for a scale
- `GET /api/contextual-chords/modes/{modeName}` - Get chords for a mode
- `GET /api/contextual-chords/voicings/{chordName}` - Get voicings for a chord

**Query Parameters:**
- `extension` - Chord extension (Triad, Seventh, Ninth, etc.)
- `stackingType` - Stacking type (Tertian, Quartal, etc.)
- `onlyNaturallyOccurring` - Filter to diatonic chords only
- `includeBorrowedChords` - Include modal interchange (NEW)
- `includeSecondaryDominants` - Include V/x chords (NEW)
- `includeSecondaryTwoFive` - Include ii-V progressions (NEW)
- `minCommonality` - Minimum probability threshold
- `limit` - Maximum results

---

### 4. **Musical Theory Implementation**

#### Diatonic Chords
- 7 chords per key (I, ii, iii, IV, V, vi, vii°)
- Marked as `IsNaturallyOccurring = true`
- Proper Roman numeral notation
- Functional analysis (Tonic, Subdominant, Dominant)

#### Borrowed Chords (Modal Interchange)
**Major Keys** - Borrow from parallel minor (Aeolian):
- iv (minor subdominant)
- bVI (flat submediant)
- bVII (flat subtonic)
- bIII (flat mediant)

**Minor Keys** - Borrow from parallel major (Ionian):
- I (major tonic)
- IV (major subdominant)
- V (major dominant)

**Characteristics:**
- Marked as `IsNaturallyOccurring = false`
- Lower commonality (probability × 0.5)
- Descriptive alternate names

#### Secondary Dominants (NEW)
Dominant chords that resolve to scale degrees other than the tonic:
- **V/ii** - Dominant of the supertonic
- **V/iii** - Dominant of the mediant
- **V/IV** - Dominant of the subdominant
- **V/V** - Dominant of the dominant
- **V/vi** - Dominant of the submediant

**Example in C Major:**
- V/V = D7 (resolves to G)
- V/IV = C7 (resolves to F)
- V/ii = A7 (resolves to Dm)

**Characteristics:**
- Calculated as perfect 5th above target
- Dominant 7th chord quality
- Moderate commonality (0.6)
- Marked with `SecondaryDominantInfo`

#### Secondary ii-V Progressions (NEW)
Common in jazz, these are ii-V progressions to scale degrees other than the tonic:
- **ii/IV-V/IV** - ii-V to the subdominant
- **ii/V-V/V** - ii-V to the dominant
- **ii/vi-V/vi** - ii-V to the submediant

**Example in C Major:**
- ii/V-V/V = Am7-D7 (resolves to G)
- ii/IV-V/IV = Gm7-C7 (resolves to F)

**Characteristics:**
- ii chord is minor 7th (Dorian mode)
- Calculated as whole step above target
- Moderate commonality (0.5)
- Marked with `IsPartOfTwoFive = true`

---

### 5. **Testing** (`Tests/GaApi.Tests/`)

#### ContextualChordServiceTests (12 tests)
- ✅ C Major returns 7 diatonic chords
- ✅ Borrowed chords included when requested
- ✅ Only naturally occurring chords when filtered
- ✅ Correct scale degrees (1-7)
- ✅ Chords ranked by commonality
- ✅ Filtering by minimum commonality
- ✅ Limit parameter respected
- ✅ Cache used correctly
- ✅ All chords have required properties
- ✅ Secondary dominants and ii-V chords handled correctly (NEW)

#### VoicingFilterServiceTests (18 tests)
- ✅ Voicing generation works
- ✅ Filtering by difficulty, fret range, open strings, barres
- ✅ Ranking by utility score
- ✅ Physical and psychoacoustic analysis
- ✅ Style tags assignment
- ✅ CAGED shape detection

**Test Results:**
```
✅ All 30 Tests Passing!
Duration: 14 seconds
```

---

## 🎯 Benefits Over Legacy System

### Musical Contextualization
1. **Proper Diatonic Chords** - Only 7 chords per key instead of all possible chords
2. **Borrowed Chords** - Modal interchange from parallel keys
3. **Secondary Dominants** - V/x chords for richer harmony (NEW)
4. **Secondary ii-V** - Jazz-style progressions (NEW)
5. **Commonality Ranking** - Chords ranked by probability in key
6. **Functional Analysis** - Tonic, subdominant, dominant functions
7. **Roman Numeral Notation** - Proper music theory notation

### Physical Contextualization
1. **Voicing Analysis** - Physical characteristics (fret span, finger stretch)
2. **Psychoacoustic Analysis** - Perceptual characteristics (consonance, brightness)
3. **Filtering Options** - Difficulty, fret range, open strings, barres
4. **Utility Ranking** - Voicings ranked by playability and sound quality
5. **Style Tags** - Jazz, Rock, Classical, etc.
6. **CAGED Shapes** - Familiar guitar patterns

---

## 📊 Performance

- **Caching**: Separate caches for regular vs semantic data
- **Cache Keys**: Include all filter parameters for proper invalidation
- **Return Types**: `List<T>` for proper cache type matching
- **Lazy Evaluation**: Chords generated on-demand
- **Filtering**: Applied after generation for flexibility

---

## 🚀 Next Steps & Roadmap

### Phase 2: Integration Tests (Not Started)
- [ ] Create integration tests for full stack
- [ ] Test API endpoints end-to-end
- [ ] Test caching behavior
- [ ] Test error handling
- [ ] Test concurrent requests

### Phase 3: Modulation Support (Partially Implemented)
- [x] Add modulation models (`ModulationInfo`, `ModulationType`)
- [x] Add filter option (`IncludeModulations`)
- [ ] Implement `GetModulationSuggestions()` method
- [ ] Identify pivot chords (chords that exist in both keys)
- [ ] Support common modulations:
  - [ ] Relative major/minor (e.g., C major ↔ A minor)
  - [ ] Parallel major/minor (e.g., C major ↔ C minor)
  - [ ] Dominant (e.g., C major → G major)
  - [ ] Subdominant (e.g., C major → F major)
  - [ ] Chromatic modulations

### Phase 4: Advanced Features (Not Started)
- [ ] Chord substitutions (tritone substitution, etc.)
- [ ] Voice leading analysis
- [ ] Chord progression suggestions
- [ ] Harmonic rhythm analysis
- [ ] Tension/release analysis
- [ ] Modal mixture (beyond simple borrowed chords)
- [ ] Extended harmony (9th, 11th, 13th chords)
- [ ] Altered dominants (b9, #9, #11, b13)

### Phase 5: Frontend Integration (Not Started)
- [ ] Update React components to use new API
- [ ] Display chord context information
- [ ] Show secondary dominants and ii-V progressions
- [ ] Display modulation suggestions
- [ ] Interactive chord progression builder
- [ ] Visual representation of harmonic function
- [ ] Voicing diagrams with analysis

### Phase 6: Performance Optimization (Not Started)
- [ ] Benchmark API performance
- [ ] Optimize chord generation algorithms
- [ ] Implement more aggressive caching
- [ ] Add database persistence for common queries
- [ ] Implement pagination for large result sets

### Phase 7: Documentation & Examples (Not Started)
- [ ] API documentation (Swagger/OpenAPI)
- [ ] Music theory guide for users
- [ ] Example chord progressions
- [ ] Tutorial videos
- [ ] Interactive examples

---

## 📝 Technical Notes

### Transposition
PitchClass transposition uses modular arithmetic:
```csharp
var transposed = PitchClass.FromValue((original.Value + semitones) % 12);
```

### Secondary Dominant Calculation
```csharp
// Perfect 5th above target
var dominantRoot = PitchClass.FromValue((targetRoot.Value + 7) % 12);
```

### Secondary ii Calculation
```csharp
// Whole step above target
var iiRoot = PitchClass.FromValue((targetRoot.Value + 2) % 12);
```

### Commonality Scoring
- Diatonic chords: Use probability from `KeyAwareChordNamingService`
- Borrowed chords: `probability × 0.5`
- Secondary dominants: `0.6` (moderate)
- Secondary ii-V: `0.5` (moderate)

---

## 🎸 Example Usage

### Get All Chords for C Major (including chromatic)
```http
GET /api/contextual-chords/keys/C%20Major?extension=Seventh&limit=50
```

### Get Only Diatonic Chords
```http
GET /api/contextual-chords/keys/C%20Major?extension=Seventh&onlyNaturallyOccurring=true
```

### Get Chords with Secondary Dominants
```http
GET /api/contextual-chords/keys/C%20Major?extension=Seventh&includeSecondaryDominants=true&includeBorrowedChords=false
```

### Get Voicings for a Chord
```http
GET /api/contextual-chords/voicings/Cmaj7?maxDifficulty=Intermediate&minFret=0&maxFret=12
```

---

## 📚 References

- **Music Theory**: Harmoniousapp.net 4-level hierarchy (Keys → Scales → Modes → Chords)
- **Secondary Dominants**: Common practice period harmony
- **Modal Interchange**: Jazz theory and contemporary harmony
- **Voicing Analysis**: Guitar pedagogy and ergonomics

---

**Last Updated**: 2025-10-18
**Version**: 1.0.0
**Status**: Phase 1 Complete ✅ | Phase 2-7 Pending

