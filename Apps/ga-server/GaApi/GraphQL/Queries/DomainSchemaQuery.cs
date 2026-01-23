namespace GaApi.GraphQL.Queries;


using GA.Domain.Core.Design;
using GA.Domain.Core.Primitives;
// Note: SchemaDiscoveryService is now in GA.Domain.Core.Design (already imported above)
using HotChocolate;
using HotChocolate.Types;

[ExtendObjectType("Query")]
public class DomainSchemaQuery([Service] SchemaDiscoveryService discoveryService)
{
    /// <summary>
    /// Discovers domain types annotated with metadata attributes (e.g. DomainRelationship, DomainInvariant).
    /// </summary>
    public IEnumerable<TypeSchemaDto> GetDomainSchema()
    {
        // Scan GA.Domain.Core assembly
        var domainAssembly = typeof(Note).Assembly;
        var schemaInfos = discoveryService.DiscoverSchema(domainAssembly);

        return schemaInfos.Select(info => new TypeSchemaDto(
            info.Name,
            info.FullName,
            info.Relationships.Select(r => new RelationshipInfoDto(
                r.TargetType.Name,
                r.TargetType.FullName ?? r.TargetType.Name,
                r.Type.ToString(),
                r.Description
            )).ToList(),
            info.Invariants.Select(i => new InvariantInfoDto(
                i.Description,
                i.Expression
            )).ToList()
        ));
    }
}

public record TypeSchemaDto(
    string Name,
    string FullName,
    List<RelationshipInfoDto> Relationships,
    List<InvariantInfoDto> Invariants);

public record RelationshipInfoDto(
    string TargetTypeName,
    string TargetTypeFullName,
    string RelationshipType,
    string Description);

public record InvariantInfoDto(
    string Description,
    string Expression);
