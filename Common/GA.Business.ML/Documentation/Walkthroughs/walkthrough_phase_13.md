# Walkthrough: Phase 13 - Modal Flavor Tagging

## Overview
Phase 13 introduced the **Modal Flavor Service**, enabling Guitar Alchemist to identify and tag voicings with their "modal personality" (e.g., Lydian, Dorian) based on characteristic notes.

## Changes
1.  **New Service**: `ModalFlavorService.cs` (in `GA.Business.ML`).
    *   Parses `Modes.yaml` to load mode definitions and characteristic intervals.
    *   Implements matching logic to tag voicings (e.g., `Flavor:Lydian` if `#4` is present).
2.  **Integration**:
    *   Updated `AutoTaggingService` to inject and use `ModalFlavorService`.
    *   Updated `Program.cs` (GaChatbot) to register the service and seed test data.
3.  **Explanation**:
    *   Updated `VoicingExplanationService` to support **Document-Level Explanation**, allowing it to explain tags that are not part of the trained vector embedding (like the new "Flavor" tags).

## Verification
I verified the implementation by running the `GaChatbot` with new seed data:
*   **Seed Cases**: Added `Cmaj7#11` (should trigger Lydian) and `Dm6` (should trigger Dorian flavor).
*   **Results**:
    *   `Cmaj7#11` -> Explained as "reflects **Lydian flavor** characteristics".
    *   `Dm6` -> Explained as "reflects **Dorian flavor** characteristics".

## Artifacts
*   `design_phase_13_modal_tagging.md`: Architectural reference.
*   `ModalFlavorService.cs`: Core implementation.

## Next Steps
*   Expand `Modes.yaml` parsing to handle recursive sub-families.
*   Consider mapping Flavor tags to new dimensions in the Vector Embedding (requires schema update v1.4).
