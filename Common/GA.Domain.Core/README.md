# GA.Domain

This project contains the **Pure Domain Entities**, **Value Objects**, and **Aggregates** for Guitar Alchemist.
It represents the "Musical Reality" — *what the domain IS*, independent of any specific application logic or business
workflow.

## Responsibilities

- **Musical Primitives**: `Note`, `Interval`, `Pitch`, `PitchClass`.
- **Music Theory Models**: `Scale`, `Mode`, `Key`, `ChordTemplate`, `ChordFormula`.
- **Instrument Models**: `Instrument`, `Tuning`, `Fretboard`, `String`.
- **Invariants**: Domain rules and validation attributes (e.g., `[DomainInvariant]`).

## Architecture

- **Layer**: 1.5 (Domain / Entities)
- **Dependencies**: `GA.Core` (Infrastructure/Utils)
- **Consumers**: `GA.Business.Core` (Logic), `GA.Business.Services` (App), `GA.Business.ML`, and all Client
  Applications.

## Design Philosophy

- **Purity**: Entities should be data-centric and behavior-rich but *service-free*. They should not depend on databases,
  heavy computation engines, or external IO.
- **Portability**: This library is designed to be lightweight and portable to any environment (Unity, Blazor WASM,
  Mobile) that needs to understand music theory.
