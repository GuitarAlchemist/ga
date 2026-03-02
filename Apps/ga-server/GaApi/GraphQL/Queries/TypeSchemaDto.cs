namespace GaApi.GraphQL.Queries;

public record TypeSchemaDto(
    string Name,
    string FullName,
    List<RelationshipInfoDto> Relationships,
    List<InvariantInfoDto> Invariants);
