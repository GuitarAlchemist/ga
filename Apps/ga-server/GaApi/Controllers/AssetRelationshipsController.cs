namespace GaApi.Controllers;

using Models;

/// <summary>
///     Controller for managing asset relationships and hierarchical data structures
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AssetRelationshipsController(
    IAssetRelationshipService relationshipService,
    ILogger<AssetRelationshipsController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get all asset relationships
    /// </summary>
    /// <returns>Complete list of asset relationships</returns>
    /// <response code="200">Returns all asset relationships</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AssetRelationship>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<AssetRelationship>>> GetAllRelationships()
    {
        try
        {
            var relationships = relationshipService.GetAllRelationships();

            var metadata = new Dictionary<string, object>
            {
                ["totalRelationships"] = relationships.Count,
                ["relationshipTypes"] = relationships
                    .GroupBy(r => r.RelationshipType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count())
            };

            return Ok(ApiResponse<List<AssetRelationship>>.Ok(relationships, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving asset relationships");
            return StatusCode(500,
                ApiResponse<List<AssetRelationship>>.Fail("Error retrieving asset relationships", ex.Message));
        }
    }

    /// <summary>
    ///     Get relationships for a specific asset type
    /// </summary>
    /// <param name="assetType">The asset type to get relationships for</param>
    /// <returns>Relationships involving the specified asset type</returns>
    /// <response code="200">Returns relationships for the asset type</response>
    /// <response code="400">Invalid asset type</response>
    [HttpGet("asset/{assetType}")]
    [ProducesResponseType(typeof(ApiResponse<List<AssetRelationship>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<List<AssetRelationship>>> GetRelationshipsForAsset(string assetType)
    {
        if (string.IsNullOrWhiteSpace(assetType))
        {
            return BadRequest(ApiResponse<object>.Fail("Asset type is required"));
        }

        try
        {
            var relationships = relationshipService.GetRelationshipsForAsset(assetType);

            var metadata = new Dictionary<string, object>
            {
                ["assetType"] = assetType,
                ["relationshipCount"] = relationships.Count,
                ["childAssets"] = relationshipService.GetChildAssetTypes(assetType),
                ["parentAssets"] = relationshipService.GetParentAssetTypes(assetType)
            };

            return Ok(ApiResponse<List<AssetRelationship>>.Ok(relationships, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving relationships for asset type: {AssetType}", assetType);
            return StatusCode(500,
                ApiResponse<List<AssetRelationship>>.Fail("Error retrieving asset relationships", ex.Message));
        }
    }

    /// <summary>
    ///     Get the complete asset hierarchy tree
    /// </summary>
    /// <returns>Hierarchical tree structure of all assets</returns>
    /// <response code="200">Returns the asset hierarchy</response>
    [HttpGet("hierarchy")]
    [ProducesResponseType(typeof(ApiResponse<AssetHierarchyNode>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<AssetHierarchyNode>> GetAssetHierarchy()
    {
        try
        {
            var hierarchy = relationshipService.BuildAssetHierarchy();

            var metadata = new Dictionary<string, object>
            {
                ["totalNodes"] = CountNodesRecursive(hierarchy),
                ["maxDepth"] = GetMaxDepthRecursive(hierarchy, 0),
                ["availableAssets"] = CountAvailableAssetsRecursive(hierarchy)
            };

            return Ok(ApiResponse<AssetHierarchyNode>.Ok(hierarchy, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building asset hierarchy");
            return StatusCode(500, ApiResponse<AssetHierarchyNode>.Fail("Error building asset hierarchy", ex.Message));
        }
    }

    /// <summary>
    ///     Get child asset types for a parent asset
    /// </summary>
    /// <param name="parentAssetType">The parent asset type</param>
    /// <returns>List of child asset types</returns>
    /// <response code="200">Returns child asset types</response>
    /// <response code="400">Invalid parent asset type</response>
    [HttpGet("children/{parentAssetType}")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<List<string>>> GetChildAssetTypes(string parentAssetType)
    {
        if (string.IsNullOrWhiteSpace(parentAssetType))
        {
            return BadRequest(ApiResponse<object>.Fail("Parent asset type is required"));
        }

        try
        {
            var childTypes = relationshipService.GetChildAssetTypes(parentAssetType);

            var metadata = new Dictionary<string, object>
            {
                ["parentAssetType"] = parentAssetType,
                ["childCount"] = childTypes.Count
            };

            return Ok(ApiResponse<List<string>>.Ok(childTypes, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving child asset types for: {ParentAssetType}", parentAssetType);
            return StatusCode(500, ApiResponse<List<string>>.Fail("Error retrieving child asset types", ex.Message));
        }
    }

    /// <summary>
    ///     Get parent asset types for a child asset
    /// </summary>
    /// <param name="childAssetType">The child asset type</param>
    /// <returns>List of parent asset types</returns>
    /// <response code="200">Returns parent asset types</response>
    /// <response code="400">Invalid child asset type</response>
    [HttpGet("parents/{childAssetType}")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<List<string>>> GetParentAssetTypes(string childAssetType)
    {
        if (string.IsNullOrWhiteSpace(childAssetType))
        {
            return BadRequest(ApiResponse<object>.Fail("Child asset type is required"));
        }

        try
        {
            var parentTypes = relationshipService.GetParentAssetTypes(childAssetType);

            var metadata = new Dictionary<string, object>
            {
                ["childAssetType"] = childAssetType,
                ["parentCount"] = parentTypes.Count
            };

            return Ok(ApiResponse<List<string>>.Ok(parentTypes, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving parent asset types for: {ChildAssetType}", childAssetType);
            return StatusCode(500, ApiResponse<List<string>>.Fail("Error retrieving parent asset types", ex.Message));
        }
    }

    /// <summary>
    ///     Get the relationship path between two asset types
    /// </summary>
    /// <param name="fromAssetType">Source asset type</param>
    /// <param name="toAssetType">Target asset type</param>
    /// <returns>Relationship path between the asset types</returns>
    /// <response code="200">Returns the relationship path</response>
    /// <response code="400">Invalid asset types</response>
    [HttpGet("path/{fromAssetType}/to/{toAssetType}")]
    [ProducesResponseType(typeof(ApiResponse<List<AssetRelationship>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<List<AssetRelationship>>> GetRelationshipPath(string fromAssetType,
        string toAssetType)
    {
        if (string.IsNullOrWhiteSpace(fromAssetType) || string.IsNullOrWhiteSpace(toAssetType))
        {
            return BadRequest(ApiResponse<object>.Fail("Both asset types are required"));
        }

        try
        {
            var path = relationshipService.GetRelationshipPath(fromAssetType, toAssetType);

            var metadata = new Dictionary<string, object>
            {
                ["fromAssetType"] = fromAssetType,
                ["toAssetType"] = toAssetType,
                ["pathLength"] = path.Count,
                ["hasDirectRelationship"] = path.Count == 1
            };

            return Ok(ApiResponse<List<AssetRelationship>>.Ok(path, metadata: metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding relationship path from {FromAssetType} to {ToAssetType}", fromAssetType,
                toAssetType);
            return StatusCode(500,
                ApiResponse<List<AssetRelationship>>.Fail("Error finding relationship path", ex.Message));
        }
    }

    /// <summary>
    ///     Get a summary of all asset types and their availability
    /// </summary>
    /// <returns>Summary of asset types</returns>
    /// <response code="200">Returns asset type summary</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> GetAssetSummary()
    {
        try
        {
            var hierarchy = relationshipService.BuildAssetHierarchy();
            var allRelationships = relationshipService.GetAllRelationships();

            var summary = new
            {
                totalAssetTypes = GetAllAssetTypesRecursive(hierarchy).Count,
                availableAssetTypes = CountAvailableAssetsRecursive(hierarchy),
                totalRelationships = allRelationships.Count,
                relationshipTypes = allRelationships
                    .GroupBy(r => r.RelationshipType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                hierarchyDepth = GetMaxDepthRecursive(hierarchy, 0),
                assetsByCategory = GetAssetsByCategoryRecursive(hierarchy)
            };

            return Ok(ApiResponse<object>.Ok(summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating asset summary");
            return StatusCode(500, ApiResponse<object>.Fail("Error generating asset summary", ex.Message));
        }
    }

    // Helper methods for hierarchy analysis
    private static int CountNodesRecursive(AssetHierarchyNode node)
    {
        return 1 + node.Children.Sum(CountNodesRecursive);
    }

    private static int GetMaxDepthRecursive(AssetHierarchyNode node, int currentDepth)
    {
        if (!node.Children.Any())
        {
            return currentDepth;
        }

        return node.Children.Max(child => GetMaxDepthRecursive(child, currentDepth + 1));
    }

    private static int CountAvailableAssetsRecursive(AssetHierarchyNode node)
    {
        var count = node.HasData ? 1 : 0;
        return count + node.Children.Sum(CountAvailableAssetsRecursive);
    }

    private static HashSet<string> GetAllAssetTypesRecursive(AssetHierarchyNode node)
    {
        var types = new HashSet<string> { node.AssetType };
        foreach (var child in node.Children)
        {
            types.UnionWith(GetAllAssetTypesRecursive(child));
        }

        return types;
    }

    private static Dictionary<string, List<string>> GetAssetsByCategoryRecursive(AssetHierarchyNode node)
    {
        var result = new Dictionary<string, List<string>>();

        if (node.Children.Any())
        {
            result[node.AssetType] = [.. node.Children.Select(c => c.AssetType)];

            foreach (var child in node.Children)
            {
                var childCategories = GetAssetsByCategoryRecursive(child);
                foreach (var kvp in childCategories)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }
}
