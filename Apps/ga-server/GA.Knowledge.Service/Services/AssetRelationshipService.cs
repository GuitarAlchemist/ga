namespace GA.Knowledge.Service.Services;

using Models;
using GA.Domain.Core.Design.Schema;
using MongoDB.Driver;

/// <summary>
///     Implementation of asset relationship service using MongoDB
/// </summary>
public class AssetRelationshipService(ILogger<AssetRelationshipService> logger, MongoDbService mongoDbService) : IAssetRelationshipService
{
    public async Task<List<AssetRelationship>> GetAllRelationshipsAsync()
    {
        logger.LogInformation("Getting all asset relationships");
        return await mongoDbService.AssetRelationships.Find(_ => true).ToListAsync();
    }

    public async Task<List<AssetRelationship>> GetRelationshipsForAssetAsync(string assetId)
    {
        logger.LogInformation("Getting relationships for asset: {AssetId}", assetId);
        var filter = Builders<AssetRelationship>.Filter.Or(
            Builders<AssetRelationship>.Filter.Eq(r => r.SourceAssetId, assetId),
            Builders<AssetRelationship>.Filter.Eq(r => r.TargetAssetId, assetId)
        );
        return await mongoDbService.AssetRelationships.Find(filter).ToListAsync();
    }

    public async Task<AssetRelationship> CreateRelationshipAsync(AssetRelationship relationship)
    {
        logger.LogInformation("Creating new relationship: {SourceId} -> {TargetId}",
            relationship.SourceAssetId, relationship.TargetAssetId);

        relationship.CreatedAt = DateTime.UtcNow;
        relationship.UpdatedAt = DateTime.UtcNow;

        await mongoDbService.AssetRelationships.InsertOneAsync(relationship);
        return relationship;
    }

    public async Task<AssetRelationship?> UpdateRelationshipAsync(string id, AssetRelationship relationship)
    {
        logger.LogInformation("Updating relationship: {Id}", id);

        var update = Builders<AssetRelationship>.Update
            .Set(r => r.SourceAssetId, relationship.SourceAssetId)
            .Set(r => r.TargetAssetId, relationship.TargetAssetId)
            .Set(r => r.RelationshipType, relationship.RelationshipType)
            .Set(r => r.Strength, relationship.Strength)
            .Set(r => r.Metadata, relationship.Metadata)
            .Set(r => r.IsBidirectional, relationship.IsBidirectional)
            .Set(r => r.Description, relationship.Description)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await mongoDbService.AssetRelationships.FindOneAndUpdateAsync(
            Builders<AssetRelationship>.Filter.Eq(r => r.Id, id),
            update,
            new FindOneAndUpdateOptions<AssetRelationship> { ReturnDocument = ReturnDocument.After }
        );

        return result;
    }

    public async Task<bool> DeleteRelationshipAsync(string id)
    {
        logger.LogInformation("Deleting relationship: {Id}", id);
        var result = await mongoDbService.AssetRelationships.DeleteOneAsync(r => r.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<AssetHierarchyNode>> GetAssetHierarchyAsync()
    {
        logger.LogInformation("Building asset hierarchy");

        // Find all nodes that are not children of any other node (roots)
        var allRelationships = await GetAllRelationshipsAsync();
        var childIds = allRelationships.Select(r => r.TargetAssetId).ToHashSet();
        
        var rootIds = allRelationships
            .Select(r => r.SourceAssetId)
            .Where(id => !childIds.Contains(id))
            .Distinct()
            .ToList();

        var roots = new List<AssetHierarchyNode>();
        foreach (var rootId in rootIds)
        {
            roots.Add(await BuildHierarchyTreeAsync(rootId));
        }

        return roots;
    }

    public async Task<AssetHierarchyNode?> GetAssetHierarchyNodeAsync(string assetId)
    {
        logger.LogInformation("Getting hierarchy node for asset: {AssetId}", assetId);

        var parentRelationships = await mongoDbService.AssetRelationships
            .Find(r => r.TargetAssetId == assetId)
            .ToListAsync();
        
        var childRelationships = await mongoDbService.AssetRelationships
            .Find(r => r.SourceAssetId == assetId)
            .ToListAsync();

        if (parentRelationships.Count == 0 && childRelationships.Count == 0)
        {
            return null;
        }

        return new AssetHierarchyNode
        {
            Id = Guid.NewGuid().ToString(),
            AssetId = assetId,
            Name = $"Asset {assetId}",
            AssetType = "node",
            ParentId = parentRelationships.FirstOrDefault()?.SourceAssetId,
            ChildIds = [.. childRelationships.Select(r => r.TargetAssetId)]
        };
    }

    public async Task<AssetHierarchyNode> BuildHierarchyTreeAsync(string rootAssetId)
    {
        logger.LogInformation("Building hierarchy tree from root: {RootAssetId}", rootAssetId);

        var rootNode = new AssetHierarchyNode
        {
            Id = Guid.NewGuid().ToString(),
            AssetId = rootAssetId,
            Name = $"Asset {rootAssetId}",
            AssetType = "root",
            Level = 0,
            Path = $"/{rootAssetId}"
        };

        var allRelationships = await GetAllRelationshipsAsync();
        var relationshipMap = allRelationships
            .Where(r => r.RelationshipType == RelationshipType.IsChildOf || r.RelationshipType == RelationshipType.IsParentOf)
            .GroupBy(r => r.SourceAssetId)
            .ToDictionary(g => g.Key, g => g.ToList());

        void BuildChildren(AssetHierarchyNode node, int currentDepth)
        {
            // Safeguard against infinite loops and extreme depth
            if (currentDepth > 10 || !relationshipMap.TryGetValue(node.AssetId, out var children))
                return;

            foreach (var rel in children)
            {
                var childNode = new AssetHierarchyNode
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetId = rel.TargetAssetId,
                    Name = $"Asset {rel.TargetAssetId}",
                    AssetType = "node",
                    ParentId = node.AssetId,
                    Level = currentDepth + 1,
                    Path = $"{node.Path}/{rel.TargetAssetId}"
                };
                
                node.ChildIds.Add(childNode.AssetId);
                node.Children.Add(childNode);
                
                BuildChildren(childNode, currentDepth + 1);
            }
        }

        BuildChildren(rootNode, 0);

        return rootNode;
    }

    public async Task<List<AssetRelationship>> FindRelatedAssetsAsync(string assetId, int maxDepth = 3)
    {
        logger.LogInformation("Finding related assets for: {AssetId} (max depth: {MaxDepth})", assetId, maxDepth);

        var allRelationships = await GetAllRelationshipsAsync();
        var results = new List<AssetRelationship>();
        var visited = new HashSet<string> { assetId };
        
        var currentLevelIds = new HashSet<string> { assetId };

        for (int depth = 0; depth < maxDepth; depth++)
        {
            var nextLevelIds = new HashSet<string>();
            foreach (var currentId in currentLevelIds)
            {
                var connectedRels = allRelationships.Where(r => 
                    r.SourceAssetId == currentId || 
                    r.TargetAssetId == currentId || 
                    (r.IsBidirectional && (r.SourceAssetId == currentId || r.TargetAssetId == currentId))
                ).ToList();

                foreach (var rel in connectedRels)
                {
                    var otherId = rel.SourceAssetId == currentId ? rel.TargetAssetId : rel.SourceAssetId;
                    if (!visited.Contains(otherId))
                    {
                        visited.Add(otherId);
                        nextLevelIds.Add(otherId);
                        results.Add(rel);
                    }
                }
            }

            if (nextLevelIds.Count == 0) break;
            currentLevelIds = nextLevelIds;
        }

        return results;
    }

    public async Task<List<string>> GetChildAssetTypesAsync(string assetId)
    {
        logger.LogInformation("Getting child asset types for: {AssetId}", assetId);

        var relationships = await GetRelationshipsForAssetAsync(assetId);
        return
        [
            .. relationships
                .Where(r => r.SourceAssetId == assetId)
                .Select(r => r.TargetAssetId)
                .Distinct()
        ];
    }

    public async Task<List<string>> GetParentAssetTypesAsync(string assetId)
    {
        logger.LogInformation("Getting parent asset types for: {AssetId}", assetId);

        var relationships = await GetRelationshipsForAssetAsync(assetId);
        return
        [
            .. relationships
                .Where(r => r.TargetAssetId == assetId)
                .Select(r => r.SourceAssetId)
                .Distinct()
        ];
    }

    public Task<AssetHierarchyNode> BuildAssetHierarchyAsync(string? assetType = null)
    {
        logger.LogInformation("Building asset hierarchy for type: {AssetType}", assetType ?? "all");

        // Assuming tree root filtering logic could be placed here.
        // For now, return the basic tree root node.
        var rootType = assetType ?? "root";
        return Task.FromResult(new AssetHierarchyNode
        {
            Id = Guid.NewGuid().ToString(),
            AssetId = $"{rootType}-hierarchy",
            Name = $"{rootType} Hierarchy",
            AssetType = rootType,
            Level = 0,
            Children = []
        });
    }

    public async Task<List<AssetRelationship>> GetRelationshipPathAsync(string sourceId, string targetId)
    {
        logger.LogInformation("Getting relationship path from {SourceId} to {TargetId}", sourceId, targetId);

        var allRelationships = await GetAllRelationshipsAsync();
        
        // BFS to find the shortest path
        var queue = new Queue<List<AssetRelationship>>();
        var visited = new HashSet<string> { sourceId };

        // Initialize queue with paths of length 1
        var initialRels = allRelationships.Where(r => 
            r.SourceAssetId == sourceId || (r.IsBidirectional && r.TargetAssetId == sourceId)).ToList();
            
        foreach (var rel in initialRels)
        {
            queue.Enqueue([rel]);
        }

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var lastRel = path.Last();
            var currentId = lastRel.SourceAssetId == sourceId || path.Count > 1 && lastRel.SourceAssetId == path[^2].TargetAssetId ? lastRel.TargetAssetId : lastRel.SourceAssetId;

            if (currentId == targetId)
            {
                return path;
            }

            visited.Add(currentId);

            var nextRels = allRelationships.Where(r => 
                (r.SourceAssetId == currentId && !visited.Contains(r.TargetAssetId)) || 
                (r.IsBidirectional && r.TargetAssetId == currentId && !visited.Contains(r.SourceAssetId))
            ).ToList();

            foreach (var rel in nextRels)
            {
                var newPath = new List<AssetRelationship>(path) { rel };
                queue.Enqueue(newPath);
            }
        }

        return [];
    }
}
