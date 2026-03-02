---
name: "OPTIC-K Schema Guardian"
description: "Maintains and validates the OPTIC-K musical embedding schema (v1.4) dimension mappings, partition weights, and vector generation logic."
---

# OPTIC-K Schema Guardian

This skill provides the definitive rules for the **OPTIC-K Musical Embedding Schema**, currently at version **1.6**. Use this skill whenever modifying or evaluating the embedding generation pipeline, similarity scoring, or RAG indexing.

## 1. Schema Overview (v1.6)
The embedding vector consists of **216 dimensions** partitioned into semantic subspaces.

| Partition | Range | Weight | Purpose |
| :--- | :--- | :--- | :--- |
| **IDENTITY** | 0-5 | N/A | Object type (Voicing, Scale, etc.). Hard filtering only. |
| **STRUCTURE** | 6-29 | **0.45** | Pitch-class set invariants (O+P+T+I). Core musical identity. |
| **MORPHOLOGY** | 30-53 | **0.25** | Physical fretboard realization (geometry/fingering). |
| **CONTEXT** | 54-65 | **0.20** | Temporal motion and harmonic functionality. |
| **SYMBOLIC** | 66-77 | **0.10** | Manual tags (Drop-2, Hendrix-style, etc.). |
| **EXTENSIONS** | 78-95 | N/A | Informational textural features (Register, Spread, Density). |
| **SPECTRAL** | 96-108 | N/A | DFT-based geometry (Phase spheres, Entropy). |
| **MODAL** | 109-148 | 0.10* | Modal flavors (28 family modes + Pentatonic/Symmetric). |
| **HIERARCHY** | 149-156 | N/A | Structural complexity and nesting depth. |
| **RESERVED** | 157-215 | N/A | Future expansion (Symbolic knowledge graphs). |

*Note: MODAL weight is typically rolled into SYMBOLIC similarity scoring.*

## 2. Validation Rules

### Identity Guard
- **Index 0-5** must be a one-hot vector indicating the object type.
- Similarity search **MUST NOT** include these indices unless filtering.

### Structure (The "Pitch Circle" Rules)
- **Chroma (6-17)**: 12-bit vector for pitch class presence.
- **ICV (19-24)**: Interval Class Vector. 
- **Consonance (29)**: Calculated via the `IntervalClassVector`. High IC5/IC3 increases this value.

### Morphology (The "Guitar" Rules)
- **Note Count (48)**: Normalized to a 6-string limit (`total / 6.0`).
- **Span (33)**: Normalized to 4 octaves (`span / 48.0`).

### Extension Formulae
If editing `MusicalEmbeddingGenerator.cs`, ensure these specific formulae are maintained:
- **Harmonic Inertia**: `Stability * (1.0 - Tension)`
- **Resolution Pressure**: `(0.7 * Tension) + (0.3 * (1.0 - Stability))`

## 3. Reference Implementation
See `Common\GA.Business.ML\Embeddings\EmbeddingSchema.cs` for the canonical C# implementation.

## 4. Usage Instructions for AntiGravity
1. **When adding a new scale**: Verify the `STRUCTURE` partition is correctly computed from the scale's `PitchClassSet`.
2. **When refactoring RAG**: Ensure the dimensionality of the vector sent to Qdrant or MongoDB matches **TotalDimension (216)**.
3. **When tuning search**: Remind the developer that `STRUCTURE` (0.45) is the primary driver of musical similarity.
