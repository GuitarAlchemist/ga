namespace GaApi.GraphQL.Queries;

using GA.Domain.Core.Primitives.Notes;
using GA.Infrastructure.Documentation;

[ExtendObjectType("Query")]
public class DomainSchemaQuery([Service] SchemaDiscoveryService discoveryService)
{
    /// <summary>
    ///     Discovers domain types annotated with metadata attributes (e.g. DomainRelationship, DomainInvariant).
    /// </summary>
    public IEnumerable<TypeSchemaDto> GetDomainSchema()
    {
        // Scan GA.Domain.Core assembly
        var domainAssembly = typeof(Note).Assembly;
        var schemaInfos = discoveryService.DiscoverSchema(domainAssembly);

        return schemaInfos.Select(info => new TypeSchemaDto(
            info.Name,
            info.FullName,
            [
                .. info.Relationships.Select(r => new RelationshipInfoDto(
                    r.TargetType.Name,
                    r.TargetType.FullName ?? r.TargetType.Name,
                    r.Type.ToString(),
                    r.Description
                ))
            ],
            [
                .. info.Invariants.Select(i => new InvariantInfoDto(
                    i.Description,
                    i.Expression
                ))
            ]
        ));
    }
}
