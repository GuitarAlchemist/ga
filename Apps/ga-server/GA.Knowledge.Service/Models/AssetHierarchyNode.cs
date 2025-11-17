namespace GA.Knowledge.Service.Models;

/// <summary>
/// Represents a node in the asset hierarchy tree
/// </summary>
public class AssetHierarchyNode
{
    /// <summary>
    /// Unique identifier for the node
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the asset this node represents
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the asset
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the asset (e.g., "chord", "scale", "progression")
    /// </summary>
    public string AssetType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the parent node (null for root nodes)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// List of child node IDs
    /// </summary>
    public List<string> ChildIds { get; set; } = new();

    /// <summary>
    /// Child nodes (populated when loading hierarchy)
    /// </summary>
    public List<AssetHierarchyNode> Children { get; set; } = new();

    /// <summary>
    /// Depth level in the hierarchy (0 for root)
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Path from root to this node
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata for the node
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Whether this node has children
    /// </summary>
    public bool HasChildren => ChildIds.Count > 0 || Children.Count > 0;

    /// <summary>
    /// Whether this is a root node
    /// </summary>
    public bool IsRoot => ParentId == null;

    /// <summary>
    /// Whether this node has data
    /// </summary>
    public bool HasData => !string.IsNullOrEmpty(AssetId) && !string.IsNullOrEmpty(Name);

    /// <summary>
    /// When the node was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the node was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
