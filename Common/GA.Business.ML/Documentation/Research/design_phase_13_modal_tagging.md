# Design: Phase 13 - Modal Flavor Tagging

## Goal
To enrich voicings with **"Flavor" tags** derived from their parent scales and modes (specifically their characteristic intervals).
This answers the user's desire to identify the "Spanish" flavor of Phrygian or the "Dreamy" flavor of Lydian chords.

## Data Source
We will leverage `GA.Business.Config/Modes.yaml`.
This file already contains rich definitions:
-   **Structure**: `IntervalClassVector`
-   **CharacteristicIntervals**: e.g., `b2` (Phrygian), `#4` (Lydian)
-   **GuitarFretboardTips**: "Milk b2 against tonic for Spanish bite"

## Core Concepts

### 1. Characteristic Intervals (Color Tones)
Every mode has a unique sonic signature defined by 1 or 2 intervals relative to the root.
-   **Lydian**: Major 3rd + **Sharp 4 (#11)**
-   **Phrygian**: Perfect 5th + **Flat 2 (b9)**
-   **Dorian**: Minor 3rd + **Major 6 (13)**
-   **Mixolydian**: Major 3rd + **Flat 7**
-   **Locrian**: Dim 5th + **Flat 2**

### 2. Flavor Matching Logic
A voicing V has "Flavor M" if:
1.  **V is a subset of Mode M**: All notes in V exist in M (relative to some root).
2.  **V contains the Characteristic Interval** of M.
    -   *Example*: A Cmaj7(#11) chord contains C-E-G-B-F#. It is a subset of C Lydian. It contains the #4 (F#). Therefore, it has **Lydian Flavor**.
    -   *Counter-Example*: A Cmaj7 chord (C-E-G-B) is a subset of C Lydian, but lacks the #4. It is "Generic Major" or "Ionian/Lydian Ambiguous". It does *not* carry the distinct Lydian flavor strongly enough to warrant the tag (or maybe it gets a weak tag).

### 3. Atonal/Modal Families
For non-diatonic modes (Messiaen, Harmonic Minor modes), we apply the same logic:
-   **Phrygian Dominant**: Major 3rd + Flat 2. Application: "Flamenco", "Spanish".

## Implementation Strategy

### New Service: `ModalFlavorService`
Located in `GA.Business.ML.Musical.Enrichment`.

**Responsibilities**:
1.  **Load Definitions**: Parse `Modes.yaml` on startup (Singleton).
    -   Cache `CharacteristicIntervals` (converted to semitones: b2=1, #4=6, etc.).
2.  **Analyze Voicing**:
    -   Input: `VoicingDocument` (Root, PitchClasses).
    -   Algorithm:
        -   Compute `IntervalSet` = { (pc - root) % 12 }.
        -   Iterate through loaded Modes.
        -   Check: Does `IntervalSet` contain *All* of Mode's `CharacteristicIntervals`?
        -   Check: Is `IntervalSet` compatible with Mode (no "Avoid Notes" or conflicting intervals like M3 vs m3)?
3.  **Generate Tags**:
    -   If Match: Add tag `Flavor:{ModeName}` (e.g., `Flavor:Lydian`).
    -   Add associated Application tags: `Style:Flamenco` (from Phrygian).

### Update `AutoTaggingService`
Inject `ModalFlavorService` and call `Enrich(doc)` during the `GenerateTags` workflow.

### Update `VoicingExplanationService`
Map `Flavor:{ModeName}` tags to the `Description` or `GuitarFretboardTips` from YAML.
-   *Explanation*: "This voicing evokes a **Lydian** sound due to the #11 interval. Perfect for film scoring."

## Config Schema Extensions
We may need to formalize the `CharacteristicIntervals` field in `Modes.yaml` to be machine-readable integers (e.g. `2`, `b2` parsing logic exists but integers `2`, `1` are safer).
Currently `Modes.yaml` uses strings: `'b2'`, `'#4'`. We need a parser for this.

## Risks & Edge Cases
-   **Ambiguity**: A "C5" power chord fits almost every mode. We should NOT tag it with every flavor. The **Constraint** is: Must contain the **Characteristic Color Tone**.
    -   C5 (C, G) has no 3rd, no 7th, no color. -> No Flavor Tag.
    -   Csus2 (C, D, G) has Major 2. -> Generic.
    -   C(add b9) (C, Db, E, G) -> Has b2. -> Phrygian Flavor.

## Validation
-   Test `Cmaj7` -> No Lydian Tag.
-   Test `Cmaj7#11` -> Lydian Tag.
-   Test `Cm6` -> Dorian Tag (because of Major 6 + Minor 3).
-   Test `C7b9` -> Phrygian Dominant Flavor (Major 3 + b2 + b7).
