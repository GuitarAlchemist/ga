namespace GaApi.GraphQL.Queries;

public record RelationshipInfoDto(
    string TargetTypeName,
    string TargetTypeFullName,
    string RelationshipType,
    string Description);
