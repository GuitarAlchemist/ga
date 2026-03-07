namespace GA.Domain.Core.Design.Schema;

/// <summary>All schema metadata for a domain type (invariants + relationships).</summary>
/// <remarks>
/// Used by the type schema reflection system to surface domain rules and relationships
/// without coupling the domain model to infrastructure concerns.
/// </remarks>
public record TypeSchemaInfo(
    string Name,
    string FullName,
    List<RelationshipInfo> Relationships,
    List<InvariantInfo> Invariants);
