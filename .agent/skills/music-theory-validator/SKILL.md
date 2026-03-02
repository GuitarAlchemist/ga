---
name: "Music Theory Validator"
description: "Ensures any new Chords, Scales, or Intervals added to the domain core are mathematically and musically correct according to the Guitar Alchemist theory engine."
---

# Music Theory Validator

This skill provides the logical constraints and musical axioms for the **Guitar Alchemist** domain. Use this when implementing new musical objects or validating generated content.

## 1. Core Musical Axioms
1. **Octave Equivalence**: All pitch classes are calculated modulo 12.
2. **Minimal Chord**: A chord MUST have at least 2 notes (dyad), though 3+ is the standard for triads.
3. **Root Prominence**: A chord always has a `Root` which defines its identity and formula.

## 2. Chord Quality Rules
The following semitone offsets from the root define triad qualities:

| Quality | Interval 1 (Semitones) | Interval 2 (Semitones) |
| :--- | :--- | :--- |
| **Major** | 4 (Major 3rd) | 7 (Perfect 5th) |
| **Minor** | 3 (Minor 3rd) | 7 (Perfect 5th) |
| **Diminished** | 3 (Minor 3rd) | 6 (Diminished 5th) |
| **Augmented** | 4 (Major 3rd) | 8 (Augmented 5th) |

## 3. Extension Logic
Extensions depend on the presence of specific interval classes:
- **7th**: Requires semitone 10 (m7) or 11 (M7).
- **9th**: Requires semitone 2 (or 14).
- **11th**: Requires semitone 5 (or 17).
- **13th**: Requires semitone 9 (or 21).

## 4. Scale Validation
- A scale is defined by its `PitchClassSet`.
- Common scales like **Prometheus** (0, 2, 4, 6, 9, 10) or **Diatonic** (7 notes) have fixed interval patterns.
- Always check consistency via `IntervalClassVector`.

## 5. Domain Invariants
- `A chord must have a root note and a pitch class set`.
- `PitchClassSet` is a tonal realization of the notes.
- Inversion indices start at 0 (Root Position).

## 6. Verification Steps for AntiGravity
Before committing a new musical definition:
1. Calculate the semitone array.
2. Match against the quality rules in Section 2.
3. Check the `Symbol` generation logic to ensure it matches `GenerateSymbol()` in `Chord.cs`.
