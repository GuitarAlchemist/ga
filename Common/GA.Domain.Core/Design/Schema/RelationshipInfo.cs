namespace GA.Domain.Core.Design.Schema;

/// <summary>Metadata about a structural relationship between domain types.</summary>
/// <remarks>
/// Used by the type schema reflection system to surface domain rules and relationships
/// without coupling the domain model to infrastructure concerns.
/// </remarks>
public record RelationshipInfo(
    Type TargetType,
    RelationshipType Type,
    string Description);
