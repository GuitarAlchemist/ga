# Specification: Core Schema & Relationship Design

## Goal
Establish a single source of truth for GA domain entities and their relationships to prevent model divergence across microservices and ensure data integrity.

## Requirements

### 1. Data Model Standardization
- Define a central directory of GA entities (Music Theory, Spatial, AI, Analytics).
- Standardize naming conventions for attributes across service boundaries.
- Mapping of relationships between core types (e.g., `PitchClassSet` -> `IntervalClassVector`).

### 2. Type Definitions
- Refine and expand `RelationshipType` enum to cover all semantic connections.
- Ensure `DomainRelationshipAttribute` is applicable to all relevant constructs (records, classes, interfaces).
- Support for "Strength" or "Probability" metadata in relationships (critical for AI/Spectral logic).

### 3. Validation Rules
- Define "Invariants" for each entity (e.g., Pitch classes must be 0-11).
- Specification of referential integrity checks between related entities.
- Programmatic validation of annotated relationships via reflection.

### 4. Documentation & Tooling
- Generate human-readable schema documentation (Markdown/Mermaid).
- Ensure schema is discoverable via code (IntelliSense/Attributes).
- Selection of schema management tools (if beyond standard .NET/MongoDB constructs).

## Success Criteria
- [ ] Comprehensive `DOMAIN_SCHEMA.md` documenting all core entities.
- [ ] Build pass for all microservices using updated shared models.
- [ ] Automated test suite verifying `DomainRelationshipAttribute` annotations.
- [ ] Standardized `Relationship` DTOs used across all 6 microservices.
