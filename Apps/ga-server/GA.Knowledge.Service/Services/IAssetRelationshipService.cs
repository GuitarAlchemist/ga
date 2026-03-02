namespace GA.Knowledge.Service.Services;

using Models;

/// <summary>
///     Service interface for managing asset relationships and hierarchies
/// </summary>
public interface IAssetRelationshipService
{
    /// <summary>
    ///     Get all asset relationships
    /// </summary>
    /// <returns>List of all relationships</returns>
    Task<List<AssetRelationship>> GetAllRelationshipsAsync();

    /// <summary>
    ///     Get relationships for a specific asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of relationships involving the asset</returns>
    Task<List<AssetRelationship>> GetRelationshipsForAssetAsync(string assetId);

    /// <summary>
    ///     Create a new relationship
    /// </summary>
    /// <param name="relationship">Relationship to create</param>
    /// <returns>Created relationship</returns>
    Task<AssetRelationship> CreateRelationshipAsync(AssetRelationship relationship);

    /// <summary>
    ///     Update an existing relationship
    /// </summary>
    /// <param name="id">Relationship ID</param>
    /// <param name="relationship">Updated relationship data</param>
    /// <returns>Updated relationship</returns>
    Task<AssetRelationship?> UpdateRelationshipAsync(string id, AssetRelationship relationship);

    /// <summary>
    ///     Delete a relationship
    /// </summary>
    /// <param name="id">Relationship ID</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteRelationshipAsync(string id);

    /// <summary>
    ///     Get asset hierarchy starting from root nodes
    /// </summary>
    /// <returns>List of root hierarchy nodes</returns>
    Task<List<AssetHierarchyNode>> GetAssetHierarchyAsync();

    /// <summary>
    ///     Get hierarchy for a specific asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>Hierarchy node for the asset</returns>
    Task<AssetHierarchyNode?> GetAssetHierarchyNodeAsync(string assetId);

    /// <summary>
    ///     Build hierarchy tree from relationships
    /// </summary>
    /// <param name="rootAssetId">Root asset ID</param>
    /// <returns>Hierarchy tree</returns>
    Task<AssetHierarchyNode> BuildHierarchyTreeAsync(string rootAssetId);

    /// <summary>
    ///     Find related assets using graph traversal
    /// </summary>
    /// <param name="assetId">Starting asset ID</param>
    /// <param name="maxDepth">Maximum traversal depth</param>
    /// <returns>List of related assets</returns>
    Task<List<AssetRelationship>> FindRelatedAssetsAsync(string assetId, int maxDepth = 3);

    /// <summary>
    ///     Get child asset types for a given asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of child asset types</returns>
    Task<List<string>> GetChildAssetTypesAsync(string assetId);

    /// <summary>
    ///     Get parent asset types for a given asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of parent asset types</returns>
    Task<List<string>> GetParentAssetTypesAsync(string assetId);

    /// <summary>
    ///     Build asset hierarchy for a specific asset type
    /// </summary>
    /// <param name="assetType">Asset type (optional)</param>
    /// <returns>Hierarchy for the asset type</returns>
    Task<AssetHierarchyNode> BuildAssetHierarchyAsync(string? assetType = null);

    /// <summary>
    ///     Get relationship path between two assets
    /// </summary>
    /// <param name="sourceId">Source asset ID</param>
    /// <param name="targetId">Target asset ID</param>
    /// <returns>Path of relationships</returns>
    Task<List<AssetRelationship>> GetRelationshipPathAsync(string sourceId, string targetId);
}
