---
name: Fretboard & Tab Integrity
description: Standards for parsing tabs, mapping coordinates, and ensuring fretboard consistency to prevent analysis errors.
---

# Fretboard & Tab Integrity Standards

Use this skill when modifying `TabAnalysisService`, `TabToPitchConverter`, or Fretboard logic.

## 1. Canonical Representation
**Single Source of Truth for Pitch.**

- **Rule**: Always convert Tab (String/Fret) -> `MidiNote` (Abs integer) -> `PitchClass` (0-11 integer) immediately.
- **Anti-Pattern**: Passing around string names like "A#" or "Bb" in the analysis pipeline.
- **Verification**: Analysis classes should take `int` or `PitchClass` inputs, never `string`.

## 2. Coordinate Safety
**Validate Physical Possibility.**

- **Rule**: All Fret coordinates must be bounded (0 to `Skipped` to `MaxFret`).
- **Rule**: All String indices must check against `Instrument.StringCount`.
- **Validation**:
  ```csharp
  if (fret < 0 && fret != -1) throw new InvalidFretException();
  if (stringIndex >= tuning.Strings.Count) throw new InvalidStringException();
  ```

## 3. Bass & Inversion Awareness
**The Lowest Note Matters.**

- **Rule**: When identifying chords from Tab, the lowest pitch (by Frequency/MidiValue) is the Bass.
- **Rule**: `IdentifyChord` must accepts an explicit `BassPitchClass`.
- **Rule**: Naming logic MUST append slash (e.g., "/E") if `Root != Bass`.

## 4. Tab Protocol
**Defensive Parsing.**

- **Rule**: Support variable string counts (4-string Bass vs 6-string Guitar vs 7-string). Don't hardcode "6".
- **Rule**: Handle "Ghost Notes" (x), "Ties", and "Rests" explicitly.
- **Rule**: All Tab Parsing tests must include a "Bad Input" case (e.g., misaligned bars, invalid chars).

## 5. Golden Set Validation
**Regression Testing.**

- **Rule**: Key musical pattern tests (like the Andalusian Cadence) are immutable.
- **Rule**: When refactoring detection logic, you MUST run the `RefactoringImplementationTests` suite.
