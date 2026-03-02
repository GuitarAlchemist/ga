namespace GA.Domain.Core.Design.Schema;

public record RelationshipInfo(
    Type TargetType,
    RelationshipType Type,
    string Description);
