# GA.Business.DSL

This project contains Domain Specific Languages (DSLs) for music theory and practice routines, following the modular architecture Layer 3 (Analysis/DSL).

## Overview

This library provides:
- **Practice Routine DSL**: A language to define and generate structured practice routines.
- **Music Theory DSL**: Parsers and generators for musical concepts expressed in text.
- **Grammars**: Antlr/FParsec-based grammars for musical notation.
- **LSP Support**: Language Server Protocol foundations for musical DSLs.

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 3 (Analysis/DSL)**: `GA.Business.DSL` (this project)
- **Dependencies**: `GA.Core`, `GA.Business.Core`
- **Consumers**: Applications and UI components.

## Services/Features

### Practice Routines
Located in `Generators/` and `Parsers/`:
- **RoutineGenerator**: Generates concrete exercises from DSL definitions.
- **RoutineParser**: Parses human-readable practice plans.

### Grammars
Located in `Grammars/`:
- **MusicalGrammar**: Core grammar for chord and scale notation.

## Usage

### Parsing a Practice Routine (F#)

```fsharp
open GA.Business.DSL.Parsers

let routine = RoutineParser.parse "Scale: Major, Key: C, Tempo: 120"
```

## Authors

Stephane Pareilleux
