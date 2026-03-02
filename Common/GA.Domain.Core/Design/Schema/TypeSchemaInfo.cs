namespace GA.Domain.Core.Design.Schema;

public record TypeSchemaInfo(
    string Name,
    string FullName,
    List<RelationshipInfo> Relationships,
    List<InvariantInfo> Invariants);
