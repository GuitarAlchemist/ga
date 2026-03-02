# GA.Core

This project contains low-level utilities, functional patterns, and mathematical foundations for the Guitar Alchemist
solution, following the modular architecture Layer 1 (Base/Infrastructure).

## Overview

This library provides:

- **Functional Programming**: Monads, Option types, and functional extensions.
- **Combinatorics**: Variations, combinations, and permutations utilities.
- **Design Patterns**: Base implementations for common patterns (e.g., Singleton, Factory).
- **Numerics**: Specialized mathematical utilities and extensions.
- **Collections**: Specialized collection types and extension methods.

## Architecture

This project follows the Guitar Alchemist modular architecture:

- **Layer 1 (Base)**: `GA.Core` (this project)
- **Dependencies**: None (External packages only)
- **Consumers**: All projects in the solution (Layers 2-5).

## Services/Features

### Functional Extensions

Located in `Functional/`:

- **Result/Option**: Functional error handling and optional value patterns.
- **Pipe/Compose**: Functional composition utilities.

### Combinatorics

Located in `Combinatorics/`:

- **Variations**: Generating variations with and without repetitions.
- **Combinations**: Standard combinatorial set generation.

### Design Patterns

Located in `DesignPatterns/`:

- **Observable**: Base classes for observable patterns.
- **Repository**: Generic repository abstractions.

## Usage

### Using Functional Extensions

```csharp
using GA.Core.Functional;

var result = Result.Success(42)
    .Map(x => x * 2)
    .Ensure(x => x > 0, "Must be positive");
```

### Combinatorial Set Generation

```csharp
using GA.Core.Combinatorics;

var variations = Variations.WithRepetition(new[] { 0, 1 }, 3);
// Result: { [0,0,0], [0,0,1], [0,1,0], ... }
```

## Authors

Stephane Pareilleux
