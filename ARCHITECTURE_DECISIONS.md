# Architecture Decisions

## 2026-01-18: Domain-Driven Design Refactoring

### Context
The project is being refactored to align with Domain-Driven Design (DDD) principles to improve maintainability, clarity, and separation of concerns.

### Decision
Adhere to a clean, DDD-oriented .NET solution structure with the following projects in `Common/`:

*   **`GA.Domain.Core`**: The conceptual bedrock. Contains Entities, Value Objects, Aggregates, Domain Events, and pure Domain Interfaces. Dependencies point inward.
*   **`GA.Domain.Services`**: Domain Services for logic that spans multiple entities or doesn't fit naturally on a single aggregate.
*   **`GA.Domain.Repositories`**: Repository interfaces (Ports).
*   **`GA.Application`**: Application Services, Use Cases (Vertical Slices), DTOs, Command/Query Handlers. Orchestrates the domain.
*   **`GA.Infrastructure`**: Implementation of Repositories, External Service Clients, EF Core mappings, Persistence, Messaging. Depends on `GA.Application` and `GA.Domain.*`.
*   **`GA.Presentation`**: API Controllers, UI Components, Composition Root.

### Principles
*   **Dependencies point inward.**
*   **Rich Domain Models**: Avoid anemic models; encapsulate logic and invariants within entities.
*   **Tactical DDD**: Use patterns (Aggregates, Value Objects) where they add value, not religiously.
*   **Vertical Slices**: Prefer vertical slices in the Application layer over horizontal layering where possible.
*   **EF Core as Adapter**: Let EF Core adapt to the domain model, not vice-versa.

### References
*   *Domain-Driven Design* - Eric Evans
*   *Implementing Domain-Driven Design* - Vaughn Vernon
*   *Clean Architecture*