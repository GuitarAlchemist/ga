# GA.Domain.Core.Tests

Test project for GA.Domain.Core domain layer.

## Structure

- **Primitives/**: Tests for core value objects (Str, Fret, Note, PitchClass, etc.)
- **Theory/Atonal/**: Tests for atonal music theory components (PitchClassSet, SetClass, etc.)
- **Theory/Tonal/**: Tests for tonal music theory components (Scale, Mode, etc.)
- **Theory/Harmony/**: Tests for harmony components (ChordTemplate, ChordFormula, etc.)
- **Instruments/**: Tests for instrument models (Fretboard, Tuning, etc.)
- **TestHelpers/**: Common test utilities and data

## Test Categories

- **Unit**: Fast unit tests for individual components
- **Integration**: Tests for component interaction
- **Performance**: Tests for performance characteristics
- **EdgeCase**: Tests for boundary conditions and error handling

## Running Tests

```bash
dotnet test Tests/GA.Domain.Core.Tests/GA.Domain.Core.Tests.csproj
```

## Test Coverage Goals

- 90%+ line coverage for core primitives
- 85%+ coverage for domain logic
- All domain invariants tested
- All edge cases covered