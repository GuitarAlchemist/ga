# Implementation Plan: Core Schema & Relationship Design

## Phase 1: Audit & Refinement (Design)
- [x] Audit existing `RelationshipType` usage in `GA.Business.Analytics` and `GA.Knowledge.Service`.
- [x] Align `string`-based relationship types with the `GA.Business.Core` enum.
- [x] Draft the initial Mermaid relationship diagram for the top 10 core entities.

## Phase 2: Core Standardization (Implementation)
- [x] Update `GA.Business.Core.Design.RelationshipType` with new members if needed (e.g., `TransformsTo`, `IsParallelTo`).
- [x] Annotate core models (`Pitch`, `Chord`, `Scale`, `Voicing`) with `DomainRelationshipAttribute`.
- [x] Implement `SchemaDiscoveryService` to extract schema info via reflection.

## Phase 3: Validation & Invariants
- [x] Port `InvariantValidationResult` and logic to a centralized location if needed.
- [x] Add `[DomainInvariant]` attribute for specifying data constraints.
- [x] Implement global validation engine.

## Phase 4: Migration & Documentation
- [ ] Update microservice controllers to use the new standardized relationship DTOs.
- [x] Generate `docs/DOMAIN_SCHEMA.md` with Mermaid diagrams.
- [x] Verify build and integration tests across all services.

## Phase 5: Verification
- [x] Build all microservices.
- [x] Run `SchemaTests.cs` and expand with new entities.
