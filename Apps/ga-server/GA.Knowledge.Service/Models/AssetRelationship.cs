namespace GA.Knowledge.Service.Models;

/// <summary>
/// Represents a relationship between two assets in the knowledge graph
/// </summary>
public class AssetRelationship
{
    /// <summary>
    /// Unique identifier for the relationship
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the source asset
    /// </summary>
    public string SourceAssetId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the target asset
    /// </summary>
    public string TargetAssetId { get; set; } = string.Empty;

    /// <summary>
    /// Type of relationship (e.g., "contains", "references", "depends_on")
    /// </summary>
    public string RelationshipType { get; set; } = string.Empty;

    /// <summary>
    /// Strength or weight of the relationship (0.0 to 1.0)
    /// </summary>
    public double Strength { get; set; }

    /// <summary>
    /// Additional metadata about the relationship
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// When the relationship was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the relationship was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the relationship is bidirectional
    /// </summary>
    public bool IsBidirectional { get; set; }

    /// <summary>
    /// Optional description of the relationship
    /// </summary>
    public string? Description { get; set; }
}
