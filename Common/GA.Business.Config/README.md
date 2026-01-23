# GA.Business.Config

This project contains the configuration-based musical knowledge base for Guitar Alchemist, including scales, modes, chords, and techniques, following the modular architecture Layer 2 (Domain/Data).

## Overview

This library provides:
- **YAML/TOML Configuration**: Static definitions for a vast array of musical concepts.
- **Strongly Typed Access**: Type providers and F# services to access configuration data.
- **Musical Knowledge Base**: Iconic chords, common progressions, and specialized tunings.
- **Instrument Definitions**: Detailed configuration for various stringed instruments.

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 2 (Core/Domain)**: `GA.Business.Config` (this project)
- **Dependencies**: `GA.Core`
- **Consumers**: `GA.Business.Core`, `GA.Business.ML`, and applications.

## Services/Features

### Musical Definitions
Located in project root (YAML files):
- **Modes/Scales**: Definitions for hundreds of musical scales and modes.
- **Chords**: Iconic chords and standard chord formulas.
- **Progressions**: Common chord progressions across genres.

### Specialized Knowledge
- **Instruments**: Tuning and configuration for guitars, ukuleles, etc.
- **Techniques**: Descriptions and properties of various playing techniques.
- **Semantic Nomenclature**: Naming conventions and semantic mappings.

## Usage

### Accessing Modes Configuration (F#)

```fsharp
open GA.Business.Config.Configuration

let allModes = ModesConfig.Modes
let majorScale = allModes |> Seq.find (fun m -> m.Name = "Major")
```

## Authors

Stephane Pareilleux
