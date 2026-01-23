namespace GA.Domain.Core.Design;

public record RelationshipInfo(
    Type TargetType,
    RelationshipType Type,
    string Description);