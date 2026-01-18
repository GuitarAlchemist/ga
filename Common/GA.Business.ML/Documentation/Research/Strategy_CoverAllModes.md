# Strategy: Covering All Modal Families (Tonal & Atonal)

## Objective
To provide a unified system that can identify and classify *any* possible collection of notes, bridging the gap between traditional **Tonal Modes** (e.g., Lydian, Dorian) and structural **Atonal Families** (Set Classes/Forte Numbers).

## The Dual-Layer Strategy

We employ a dual-layer approach to ensure 100% coverage while prioritizing semantic meaning.

### Layer 1: Semantic Modes (Tonal/Cultural)
**Goal:** Identify culturally significant modes with specific functional names.
**Coverage:** ~117 named modes (Diatonic, Harmonic/Melodic Minor, Exotic, Pentatonic, Symmetric).
**Mechanism:** `ModalFlavorService`
- **Logic:** Matches "Characteristic Intervals" (e.g., Lydian = #4).
- **Conflict Resolution:** Uses full interval sets to penalize conflicts (notes in voicing that contradict the mode).
- **Prioritization:** "Standard" modes (Church modes) are preferred over "Exotic" modes when both match, unless the exotic match is significantly better.

### Layer 2: Structural Families (Atonal/Mathematical)
**Goal:** Classify *every possible* note collection based on its interval structure.
**Coverage:** 100% (All 224 Prime Forms / Forte Numbers).
**Mechanism:** `ForteCatalog` + `AutoTaggingService`
- **Logic:** Reduces any voicing to its **Prime Form** (Pitch Class Set invariant).
- **Labeling:** Assigns a **Forte Number** (e.g., "7-35" for Diatonic, "3-11" for Major/Minor Triad).
- **Bridge:** Common modes are just rotations of specific Forte sets.
    - Major/Dorian/Lydian → **7-35**
    - Harmonic Minor → **7-32**
    - Melodic Minor → **7-34**
    - Whole Tone → **6-35**

## Implementation Plan

### 1. Refine Tonal Identification
- **Completed:** Implemented `ModalCharacteristicIntervalService` to load 117 modes programmatically.
- **Completed:** Updated `ModalFlavorService` to use conflict-based scoring (Score = Matches - Conflicts) and priority tiers to favor standard names.

### 2. Integrate Atonal Identification
- **Next Step:** Update `AutoTaggingService` (and `TabAnalysisService`) to compute and store the **Forte Number** for every analyzed voicing.
- **Result:** Every voicing will have a structural tag (e.g., `Set:7-35`) regardless of whether it has a semantic flavor name.

### 3. Unified User Experience
- **Chatbot Response:**
    - "This is a **Lydian** voicing." (Semantic Layer)
    - "Structurally, it belongs to the **Diatonic Family (7-35)**." (Structural Layer)
    - If no named mode matches: "This is an unidentified structure (**Forte 5-Z12**)."

## Coverage Verification
- **Tonal:** Verified via `ModalFlavorTests` (e.g., distinguishing Mixolydian from Enigmatic Lydian).
- **Atonal:** Verified via `ProgrammaticForteCatalogTests` (Core library guarantees 224/224 coverage).