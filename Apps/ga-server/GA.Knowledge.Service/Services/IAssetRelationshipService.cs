using GA.Knowledge.Service.Models;

namespace GA.Knowledge.Service.Services;

/// <summary>
/// Service interface for managing asset relationships and hierarchies
/// </summary>
public interface IAssetRelationshipService
{
    /// <summary>
    /// Get all asset relationships
    /// </summary>
    /// <returns>List of all relationships</returns>
    List<AssetRelationship> GetAllRelationships();

    /// <summary>
    /// Get relationships for a specific asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of relationships involving the asset</returns>
    List<AssetRelationship> GetRelationshipsForAsset(string assetId);

    /// <summary>
    /// Create a new relationship
    /// </summary>
    /// <param name="relationship">Relationship to create</param>
    /// <returns>Created relationship</returns>
    AssetRelationship CreateRelationship(AssetRelationship relationship);

    /// <summary>
    /// Update an existing relationship
    /// </summary>
    /// <param name="id">Relationship ID</param>
    /// <param name="relationship">Updated relationship data</param>
    /// <returns>Updated relationship</returns>
    AssetRelationship UpdateRelationship(string id, AssetRelationship relationship);

    /// <summary>
    /// Delete a relationship
    /// </summary>
    /// <param name="id">Relationship ID</param>
    /// <returns>True if deleted successfully</returns>
    bool DeleteRelationship(string id);

    /// <summary>
    /// Get asset hierarchy starting from root nodes
    /// </summary>
    /// <returns>List of root hierarchy nodes</returns>
    List<AssetHierarchyNode> GetAssetHierarchy();

    /// <summary>
    /// Get hierarchy for a specific asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>Hierarchy node for the asset</returns>
    AssetHierarchyNode? GetAssetHierarchyNode(string assetId);

    /// <summary>
    /// Build hierarchy tree from relationships
    /// </summary>
    /// <param name="rootAssetId">Root asset ID</param>
    /// <returns>Hierarchy tree</returns>
    AssetHierarchyNode BuildHierarchyTree(string rootAssetId);

    /// <summary>
    /// Find related assets using graph traversal
    /// </summary>
    /// <param name="assetId">Starting asset ID</param>
    /// <param name="maxDepth">Maximum traversal depth</param>
    /// <returns>List of related assets</returns>
    List<AssetRelationship> FindRelatedAssets(string assetId, int maxDepth = 3);

    /// <summary>
    /// Get child asset types for a given asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of child asset types</returns>
    List<string> GetChildAssetTypes(string assetId);

    /// <summary>
    /// Get parent asset types for a given asset
    /// </summary>
    /// <param name="assetId">Asset ID</param>
    /// <returns>List of parent asset types</returns>
    List<string> GetParentAssetTypes(string assetId);

    /// <summary>
    /// Build asset hierarchy for a specific asset type
    /// </summary>
    /// <param name="assetType">Asset type (optional)</param>
    /// <returns>Hierarchy for the asset type</returns>
    AssetHierarchyNode BuildAssetHierarchy(string? assetType = null);

    /// <summary>
    /// Get relationship path between two assets
    /// </summary>
    /// <param name="sourceId">Source asset ID</param>
    /// <param name="targetId">Target asset ID</param>
    /// <returns>Path of relationships</returns>
    List<AssetRelationship> GetRelationshipPath(string sourceId, string targetId);
}
