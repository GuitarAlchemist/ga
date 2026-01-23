# Core Schema & Relationship Design

Architectural spike to formalize and document the GA domain schema, focusing on entity relationships and validation.

## Metadata
- **Track ID**: `core-schema-design`
- **Status**: Active
- **Owner**: Gemini
- **Stack**: .NET 10, GA.Business.Core, MongoDB

## Index
- [Specification](./spec.md)
- [Implementation Plan](./plan.md)

## Key Concepts
- `RelationshipType`: Enum defining structural connections (Parent, Child, Peer, Metadata, Groups).
- `DomainRelationshipAttribute`: Attribute for programmatic schema annotation.
- `Schema Documentation`: Automatically generated or manually curated domain mapping.
