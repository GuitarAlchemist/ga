# Core Libraries Specification

This track governs the foundational .NET libraries defined in the `Core Libraries` folder of the solution.

## Layer 1: Primitives
**Project:** `GA.Core`
**Role:** Low-level, zero-dependency primitives.
**Key Namespaces:**
- `Combinatorics`: Permutations, combinations.
- `Numerics`: Math extensions.
- `Collections`: Optimized data structures.
- `Functional`: Result/Option types.

## Layer 2: Domain Logic
**Projects:** 
- `GA.Business.Core`
- `GA.Business.Core.Generated` (F# Type Providers)
**Role:** Pure music theory engine and business rules.
**Key Domains:**
- **Tonal:** Notes, Intervals, Keys, Scales, Modes.
- **Atonal:** Pitch Class Sets (Post-tonal theory).
- **Fretboard:** Navigation logic for stringed instruments.
**Architecture:**
- Stateless, immutable designs preferred.
- No dependencies on AI or Database layers.

## Layer 3: Configuration & DSLs
**Projects:**
- `GA.Business.Config` (F#)
- `GA.Business.DSL` (F#)
**Role:** Configuration definitions and Domain Specific Languages for describing musical structures.

## Layer 4: AI & Machine Learning
**Project:** `GA.Business.ML`
**Status:** **Active Development** (Targeting .NET 10)
**Role:** Centralized AI services and **Harmonic Intelligence**.

### Core Architecture: OPTIC-K (v1.3.1)
The system uses a **109-dimensional embedding space** to represent musical objects.
- **Partitions:**
  - **IDENTITY (6):** Hard type filters.
  - **STRUCTURE (24):** Interval Class Vectors (ICV).
  - **MORPHOLOGY (24):** Physical fretboard realization.
  - **CONTEXT (12):** Temporal motion/function.
  - **SYMBOLIC (12):** Style tags.
  - **SPECTRAL (13):** DFT Magnitudes & Phases (Phase Sphere).
  - **EXTENSIONS (18):** Derived features.

### Spectral RAG
A "Hallucination-Free" architecture where the LLM narrates ground-truth data retrieved via geometric vector search.
- **Phase Sphere:** Geometric space for calculating harmonic distance/tension.
- **Wavelets (Planned):** DWT for temporal progression analysis.

## Interactive Extensions
**Projects:** `GA.Interactive`, `GA.InteractiveExtension`
**Role:** .NET Interactive (Jupyter/Polyglot Notebook) integration for visualizing theory in notebooks.