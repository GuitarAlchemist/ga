# GA.Business.Core

This project contains the core music theory domain models and logic for Guitar Alchemist, following the modular
architecture Layer 2 (Domain/Core).

## Overview

This library provides:

- **Tonal Music Theory**: Notes, Pitches, Intervals, Scales, and Modes.
- **Atonal Music Theory**: Pitch classes, Pitch class sets, and Interval Class Vectors.
- **Instrument Modeling**: Detailed support for fretted instruments (Guitar, Bass, Banjo, Ukulele, etc.) and tunings.
- **Fretboard Analysis**: Geometric and logical representation of the fretboard, including voicing analysis and
  fingering.
- **Tablature Support**: Models and logic for tablature representation and conversion.

## Architecture

This project follows the Guitar Alchemist modular architecture:

- **Layer 2 (Core/Domain)**: `GA.Business.Core` (this project)
- **Dependencies**: `GA.Core` (Layer 1), `GA.Business.Config`
- **Consumers**: `GA.Business.ML` (Layer 4), `GA.Business.Intelligence`, and all higher-level applications.

## Services/Features

### Tonal Foundations

Located in `Notes/`, `Intervals/`, `Scales/`, `Tonal/`:

- **Note**: Chromatic and diatonic note representations.
- **Interval**: Pitch and degree-based intervals.
- **Scale/Mode**: Comprehensive catalog of scales and modes (Major, Harmonic Minor, Melodic Minor, etc.).

### Atonal & Mathematical Theory

Located in `Atonal/`:

- **PitchClassSet**: Set theory operations for musical analysis.
- **IntervalClassVector**: Harmonic content analysis of pitch sets.
- **ForteNumber**: Standard classification of pitch class sets.

### Fretboard & Instruments

Located in `Fretboard/`, `Chords/`:

- **Instrument**: Configurable instrument models with various tunings.
- **Fretboard**: Mapping of notes to fretboard positions.
- **Voicing**: Analysis and generation of chord voicings.

## Usage

### Working with Notes and Intervals

```csharp
using GA.Business.Core.Notes;
using GA.Business.Core.Intervals;

var cNote = Note.C;
var majorThird = Interval.MajorThird;
var eNote = cNote + majorThird;
```

### Analyzing Pitch Class Sets

```csharp
using GA.Business.Core.Atonal;

var cMajor = new PitchClassSet([0, 4, 7]);
var icv = cMajor.IntervalClassVector;
Console.WriteLine($"ICV of C Major: {icv}");
```

## Authors

Stephane Pareilleux
