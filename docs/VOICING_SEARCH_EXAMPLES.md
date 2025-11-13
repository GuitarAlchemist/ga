# Voicing Search Examples - Semantic Query Guide

This guide demonstrates how to use the Guitar Alchemist voicing search API with natural language queries. The search uses semantic embeddings to understand musical concepts and return relevant guitar voicings.

## Table of Contents

1. [Basic Searches](#basic-searches)
2. [Skill Level Searches](#skill-level-searches)
3. [Musical Style Searches](#musical-style-searches)
4. [Technical Searches](#technical-searches)
5. [Tonal Quality Searches](#tonal-quality-searches)
6. [Position-Based Searches](#position-based-searches)
7. [Advanced Combinations](#advanced-combinations)

---

## Basic Searches

### Simple Chord Searches

**Query**: `"easy jazz chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=easy+jazz+chord&limit=5"
```

**Why it's useful**: Perfect for beginners learning jazz. Returns accessible voicings that sound jazzy without requiring advanced technique.

**Expected results**: Simple 3-4 note voicings with jazz extensions (7ths, 9ths) in comfortable positions.

---

**Query**: `"major chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=major+chord&limit=10"
```

**Why it's useful**: Fundamental building block for all guitarists. Returns various major chord voicings across the fretboard.

**Expected results**: Mix of open position, barre chords, and upper position major triads and 7th chords.

---

## Skill Level Searches

### Beginner-Friendly Voicings

**Query**: `"beginner friendly open position"`
```bash
curl "http://localhost:5232/api/voicings/search?q=beginner+friendly+open+position&limit=10"
```

**Why it's useful**: Essential for guitar students starting out. These voicings use open strings and are easier to finger.

**Musical context**: 
- Great for strumming patterns
- Foundation for learning chord progressions
- Builds finger strength and dexterity

**Expected results**: Open position chords (C, G, D, A, E, Am, Em, Dm) with minimal stretching.

---

**Query**: `"simple two note chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=simple+two+note+chord&limit=10"
```

**Why it's useful**: Power chords and dyads are fundamental for rock, punk, and metal. Easy to play and move around.

**Musical context**:
- Rock rhythm guitar
- Punk progressions
- Metal riffs
- Minimalist accompaniment

**Expected results**: Root-fifth power chords, octaves, and simple intervals.

---

### Intermediate Voicings

**Query**: `"moveable chord shape"`
```bash
curl "http://localhost:5232/api/voicings/search?q=moveable+chord+shape&limit=10"
```

**Why it's useful**: Understanding moveable shapes unlocks the entire fretboard. Same fingering pattern works in any key.

**Musical context**:
- Transposing songs to different keys
- Playing in different positions
- Understanding CAGED system

**Expected results**: Barre chords, closed position voicings that can slide up/down the neck.

---

## Musical Style Searches

### Jazz Voicings

**Query**: `"rootless jazz voicing"`
```bash
curl "http://localhost:5232/api/voicings/search?q=rootless+jazz+voicing&limit=10"
```

**Why it's useful**: Essential for jazz comping. Omitting the root creates space for the bass player and emphasizes chord color.

**Musical context**:
- Jazz combo playing (guitar + bass + drums)
- Bebop and modern jazz
- Chord melody arrangements
- Walking bass accompaniment

**Expected results**: 3-4 note voicings with 3rd, 7th, and extensions (9th, 11th, 13th) but no root.

---

**Query**: `"drop 2 voicing"`
```bash
curl "http://localhost:5232/api/voicings/search?q=drop+2+voicing&limit=10"
```

**Why it's useful**: Drop-2 voicings are the foundation of jazz guitar. They create smooth voice leading and work great for chord melodies.

**Musical context**:
- Jazz standards
- Chord melody arrangements
- Voice leading exercises
- Wes Montgomery style

**Expected results**: 4-note voicings where the second-highest note is dropped an octave, creating a singable top voice.

---

**Query**: `"jazz chord with extensions"`
```bash
curl "http://localhost:5232/api/voicings/search?q=jazz+chord+with+extensions&limit=10"
```

**Why it's useful**: Extensions (9ths, 11ths, 13ths) add color and sophistication to jazz harmony.

**Musical context**:
- Modern jazz
- Fusion
- Sophisticated pop/R&B
- Neo-soul

**Expected results**: Maj7#11, Dom7b9, Min11, Maj13 voicings with rich harmonic content.

---

### Blues Voicings

**Query**: `"blues chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=blues+chord&limit=10"
```

**Why it's useful**: Blues voicings have a distinctive sound with dominant 7ths and added 6ths.

**Musical context**:
- 12-bar blues
- Blues rock
- R&B
- Soul music

**Expected results**: Dominant 7th chords, 9th chords, and voicings with the characteristic blues sound.

---

### Rock Voicings

**Query**: `"barre chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=barre+chord&limit=10"
```

**Why it's useful**: Barre chords are essential for rock guitar. They're loud, full-sounding, and moveable.

**Musical context**:
- Rock rhythm guitar
- Punk rock
- Alternative rock
- Power ballads

**Expected results**: Full 6-string and 5-string barre chord shapes (E-shape and A-shape).

---

## Technical Searches

### Voice Leading

**Query**: `"smooth voice leading"`
```bash
curl "http://localhost:5232/api/voicings/search?q=smooth+voice+leading&limit=10"
```

**Why it's useful**: Smooth voice leading creates musical, connected chord progressions where notes move minimally between chords.

**Musical context**:
- Jazz ballads
- Classical guitar
- Fingerstyle arrangements
- Chord melody

**Expected results**: Voicings that share common tones and have minimal movement between chord changes.

---

### Fingerstyle

**Query**: `"fingerstyle chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=fingerstyle+chord&limit=10"
```

**Why it's useful**: Fingerstyle voicings are optimized for picking individual notes rather than strumming.

**Musical context**:
- Acoustic fingerstyle
- Classical guitar
- Travis picking
- Chord melody

**Expected results**: Open voicings with good note separation, often using open strings.

---

## Tonal Quality Searches

### Brightness and Darkness

**Query**: `"bright voicing"`
```bash
curl "http://localhost:5232/api/voicings/search?q=bright+voicing&limit=10"
```

**Why it's useful**: Bright voicings emphasize higher frequencies and create an uplifting, energetic sound.

**Musical context**:
- Pop music
- Funk rhythm guitar
- Upbeat songs
- Major key progressions

**Expected results**: Voicings with high notes on top, major 7ths, major 9ths, and bright intervals.

---

**Query**: `"dark minor chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=dark+minor+chord&limit=10"
```

**Why it's useful**: Dark voicings create moody, introspective atmospheres.

**Musical context**:
- Minor key ballads
- Film noir soundtracks
- Gothic rock
- Melancholic songs

**Expected results**: Minor voicings with low bass notes, minor 7ths, and darker intervals.

---

**Query**: `"warm sounding chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=warm+sounding+chord&limit=10"
```

**Why it's useful**: Warm voicings have a rich, full sound without being too bright or dark.

**Musical context**:
- Jazz ballads
- Soul music
- Acoustic singer-songwriter
- Intimate settings

**Expected results**: Mid-range voicings with 7ths, 9ths, and balanced intervals.

---

## Position-Based Searches

### Open Position

**Query**: `"open string chord"`
```bash
curl "http://localhost:5232/api/voicings/search?q=open+string+chord&limit=10"
```

**Why it's useful**: Open strings create a ringing, resonant sound characteristic of acoustic guitar.

**Musical context**:
- Folk music
- Country guitar
- Acoustic pop
- Fingerstyle arrangements

**Expected results**: Chords incorporating open strings (E, A, D, G, B, E) for a full, resonant sound.

---

### Upper Positions

**Query**: `"high position voicing"`
```bash
curl "http://localhost:5232/api/voicings/search?q=high+position+voicing&limit=10"
```

**Why it's useful**: Upper position voicings cut through a mix and create a different tonal color.

**Musical context**:
- Lead guitar fills
- Chord melody (high register)
- Orchestral arrangements
- Layered guitar parts

**Expected results**: Voicings starting around fret 7 and higher, with a brighter, more focused sound.

---

## Advanced Combinations

### Complex Queries

**Query**: `"seventh chord intermediate 5th position"`
```bash
curl "http://localhost:5232/api/voicings/search?q=seventh+chord+intermediate+5th+position&limit=10"
```

**Why it's useful**: Combines multiple criteria for very specific voicing needs.

**Musical context**: Finding specific voicings for arrangements or teaching specific concepts.

---

**Query**: `"sus chord with open strings"`
```bash
curl "http://localhost:5232/api/voicings/search?q=sus+chord+with+open+strings&limit=10"
```

**Why it's useful**: Suspended chords create tension and release. Open strings add resonance.

**Musical context**:
- Modern worship music
- Ambient guitar
- Post-rock
- Atmospheric soundscapes

**Expected results**: Sus2 and Sus4 voicings incorporating open strings for a shimmering effect.

---

## Tips for Effective Searching

### 1. **Use Musical Descriptors**
Instead of technical terms only, describe the sound or feeling:
- "warm", "bright", "dark", "mellow", "aggressive"
- "smooth", "crunchy", "clean", "rich"

### 2. **Combine Concepts**
Mix technical and musical terms:
- "easy jazz chord" (skill + style)
- "bright major voicing" (tone + harmony)
- "rootless bebop chord" (technique + style)

### 3. **Specify Context**
Add context about how you'll use it:
- "fingerstyle chord" (playing technique)
- "comping voicing" (musical role)
- "chord melody" (arrangement style)

### 4. **Experiment with Synonyms**
The semantic search understands related concepts:
- "beginner" = "easy" = "simple"
- "jazz" = "bebop" = "swing"
- "bright" = "shimmering" = "clear"

---

## API Parameters

### Basic Search
```bash
GET /api/voicings/search?q={query}&limit={number}
```

### With Filters
```bash
GET /api/voicings/search?q={query}&limit={number}&difficulty={level}&position={pos}
```

**Parameters**:
- `q`: Natural language query (required)
- `limit`: Number of results (default: 10, max: 50)
- `difficulty`: Filter by skill level (Beginner, Intermediate, Advanced)
- `position`: Filter by fretboard position (Open, Middle, Upper)
- `voicingType`: Filter by type (Triad, Seventh, Extended, etc.)

---

## Performance Notes

- **Index size**: 667,125 voicings
- **Average search time**: ~2.3 seconds (ILGPU GPU-accelerated)
- **Embedding model**: mxbai-embed-large (768 dimensions)
- **GPU**: NVIDIA GeForce RTX 3070

---

**Last Updated**: 2025-11-12  
**API Version**: v1.0  
**Status**: Production Ready ✅

