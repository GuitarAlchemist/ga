# Guitar Alchemist - Architecture

This repository follows a Domain-Driven Design (DDD) architecture.

## Projects

*   **`Common/GA.Core`**: Core abstractions, extensions, and utilities.
*   **`Common/GA.Domain.Core`**: The conceptual bedrock. Contains Entities, Value Objects, Aggregates, Domain Events, and pure Domain Interfaces. Dependencies point inward.
*   **`Common/GA.Domain.Services`**: Domain Services for logic that spans multiple entities or doesn't fit naturally on a single aggregate.
*   **`Common/GA.Domain.Repositories`**: Repository interfaces (Ports).
*   **`Common/GA.Application`**: Application Services, Use Cases (Vertical Slices), DTOs, Command/Query Handlers. Orchestrates the domain.
*   **`Common/GA.Infrastructure`**: Implementation of Repositories, External Service Clients, EF Core mappings, Persistence, Messaging. Depends on `GA.Application` and `GA.Domain.*`.
*   **`Common/GA.Business.Config`**: Configuration models (F#).

## Key Concepts

*   **Ubiquitous Language**: The code uses terms from music theory (e.g., `PitchClass`, `Interval`, `ScaleMode`, `Voicing`) consistent with the domain.
*   **Rich Domain Models**: Logic is encapsulated within domain entities (e.g., `PitchClassSet` handles set theory operations).
*   **Immutability**: Value objects and many domain entities are immutable.

## Legacy Notes

The previous `GA.Business.Core` and `GA.Domain` projects have been refactored into `GA.Domain.Core` and `GA.Domain.Services`.
