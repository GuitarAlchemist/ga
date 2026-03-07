# Guitar Alchemist - Common Libraries

This directory contains all shared libraries organized by the five-layer dependency model.
Each layer may only depend on layers below it.

## Layer Map

| Layer | Project(s) | Purpose |
|---|---|---|
| **1 – Core** | `GA.Core`, `GA.Domain.Core` | Pure domain primitives: Note, Interval, PitchClass, Fretboard types |
| **2 – Domain** | `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` | Business logic, YAML configuration, BSP geometry |
| **3 – Analysis** | `GA.Domain.Services`, `GA.Business.DSL`, `GA.Business.Core.Analysis.Gpu` | Chord/scale analysis, voice leading, spectral/topological analysis |
| **4 – AI/ML** | `GA.Business.ML` | Semantic indexing, Ollama/ONNX embeddings, vector search, tab solving |
| **5 – Orchestration** | `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence` | High-level workflows, chatbot orchestration, curation |

## Which project do I add code to?

- **New music theory primitive** (Note variant, Interval type, Chord type) → `GA.Domain.Core`
- **Business rule or service** → `GA.Business.Core`
- **Analysis algorithm** (voice leading, chord detection) → `GA.Domain.Services`
- **AI/ML feature** (embeddings, vector search, agents) → `GA.Business.ML`
- **Orchestration workflow** (multi-step pipelines, agent coordination) → `GA.Business.Core.Orchestration`

## Key Concepts

- **Ubiquitous Language**: The code uses terms from music theory (`PitchClass`, `Interval`, `ScaleMode`, `Voicing`) consistent with the domain.
- **Rich Domain Models**: Logic is encapsulated within domain types (`PitchClassSet` handles set theory operations).
- **Immutability**: Value objects and many domain entities are immutable records.

## Parked Projects

These projects contain parked/excluded code preserved for future reference:
- `GA.Business.Configuration` — configuration watcher service
- `GA.Business.Analytics` — musical analytics services
- `GA.Business.Personalization` — user personalization services
