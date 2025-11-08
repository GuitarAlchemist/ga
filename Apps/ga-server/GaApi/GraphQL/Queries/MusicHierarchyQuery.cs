namespace GaApi.GraphQL.Queries;

using Models;
using Services;

/// <summary>
///     GraphQL queries for the music hierarchy navigator.
/// </summary>
[ExtendObjectType("Query")]
public class MusicHierarchyQuery
{
    [GraphQLDescription("Get descriptive metadata for each hierarchy level.")]
    public IReadOnlyList<MusicHierarchyLevelInfo> GetMusicHierarchyLevels(
        [Service] MusicHierarchyService service)
    {
        return service.GetLevels();
    }

    [GraphQLDescription("Get nodes for a hierarchy level, optionally scoped to a parent node.")]
    public IReadOnlyList<MusicHierarchyItem> GetMusicHierarchyItems(
        [Service] MusicHierarchyService service,
        MusicHierarchyLevel level,
        string? parentId,
        int take = 200,
        string? search = null)
    {
        return service.GetItems(level, parentId, take, search);
    }
}
