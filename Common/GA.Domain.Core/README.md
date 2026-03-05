# GA.Domain.Core

Pure domain primitives for Guitar Alchemist. This is **Layer 1** in the five-layer model —
the foundation everything else depends on. No business logic, no I/O, no services.

## Folder Guide

| Folder | Contents |
|---|---|
| `Primitives/` | Note, Interval, PitchClass, Semitones, Formulas — raw musical building blocks |
| `Theory/` | Chord, Scale, Mode, Key, PitchClassSet, Harmony, Atonal structures |
| `Instruments/` | Fretboard, Tuning, Position, Voicing, Biomechanics |
| `Design/` | Domain metadata attributes (`[DomainInvariant]`, `[DomainRelationship]`) and schema reflection types |

## Key Domain Relationships

```
Note → PitchClass → Interval → ChordFormula → Chord
Note → Pitch → Octave
Scale/Mode → PitchClassSet → IntervalClassVector → ModalFamily
Tuning → Fretboard → Position → Voicing
```

## Responsibilities

- **Musical Primitives**: `Note`, `Interval`, `Pitch`, `PitchClass`
- **Music Theory Models**: `Scale`, `Mode`, `Key`, `ChordTemplate`, `ChordFormula`
- **Instrument Models**: `Instrument`, `Tuning`, `Fretboard`, `String`
- **Invariants**: Domain rules and validation attributes (e.g., `[DomainInvariant]`)

## Architecture

- **Layer**: 1 (Core — alongside `GA.Core`)
- **Dependencies**: `GA.Core`
- **Consumers**: `GA.Business.Core`, `GA.Business.Config`, `GA.Business.ML`, and all applications

## Conventions

- **Sealed by default.** All concrete types are `sealed` unless inheritance is intentional.
- **Records for value objects.** Prefer `sealed record` over `sealed class` for immutable domain types.
- **No services.** If it needs a service, it belongs in `GA.Domain.Services` (Layer 3).
- **XML docs on all public types.** Every public type needs at minimum a `<summary>`.

## Design Philosophy

- **Purity**: Types should be data-centric and behavior-rich but service-free. No databases, computation engines, or external I/O.
- **Portability**: Lightweight and portable to any environment (Unity, Blazor WASM, Mobile) that needs to understand music theory.
