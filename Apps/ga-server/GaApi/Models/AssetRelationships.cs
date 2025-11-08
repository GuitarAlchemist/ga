namespace GaApi.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
///     Defines the relationship types between musical assets
/// </summary>
public enum AssetRelationshipType
{
    /// <summary>
    ///     Parent contains child assets (e.g., Scale contains Notes)
    /// </summary>
    Contains,

    /// <summary>
    ///     Asset is derived from another (e.g., Chord derived from Scale)
    /// </summary>
    DerivedFrom,

    /// <summary>
    ///     Assets are related but not hierarchical (e.g., Chord relates to Key)
    /// </summary>
    RelatedTo,

    /// <summary>
    ///     Asset is a specific instance of another (e.g., C Major is instance of Major Scale)
    /// </summary>
    InstanceOf,

    /// <summary>
    ///     Asset is used to build another (e.g., Intervals used to build Chords)
    /// </summary>
    BuildsInto,

    /// <summary>
    ///     Assets are equivalent or synonymous
    /// </summary>
    EquivalentTo
}

/// <summary>
///     Defines a relationship between two asset types
/// </summary>
public class AssetRelationship
{
    /// <summary>
    ///     The parent/source asset type
    /// </summary>
    [Required]
    public string ParentAssetType { get; set; } = string.Empty;

    /// <summary>
    ///     The child/target asset type
    /// </summary>
    [Required]
    public string ChildAssetType { get; set; } = string.Empty;

    /// <summary>
    ///     The type of relationship
    /// </summary>
    public AssetRelationshipType RelationshipType { get; set; }

    /// <summary>
    ///     Human-readable description of the relationship
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this relationship is bidirectional
    /// </summary>
    public bool IsBidirectional { get; set; }

    /// <summary>
    ///     Cardinality of the relationship (e.g., "1:many", "many:many")
    /// </summary>
    public string Cardinality { get; set; } = "1:many";

    /// <summary>
    ///     UI hints for displaying this relationship
    /// </summary>
    public AssetRelationshipUiHints UiHints { get; set; } = new();
}

/// <summary>
///     UI hints for displaying asset relationships
/// </summary>
public class AssetRelationshipUiHints
{
    /// <summary>
    ///     Suggested display order (lower numbers first)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    ///     Whether to show this relationship in tree views
    /// </summary>
    public bool ShowInTreeView { get; set; } = true;

    /// <summary>
    ///     Whether to show this relationship in detail views
    /// </summary>
    public bool ShowInDetailView { get; set; } = true;

    /// <summary>
    ///     Icon to use for this relationship type
    /// </summary>
    public string Icon { get; set; } = "fas fa-link";

    /// <summary>
    ///     CSS class for styling
    /// </summary>
    public string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this relationship should be expanded by default in UI
    /// </summary>
    public bool ExpandedByDefault { get; set; } = false;

    /// <summary>
    ///     Maximum number of items to show before pagination
    /// </summary>
    public int MaxDisplayItems { get; set; } = 50;
}

/// <summary>
///     Service for managing asset relationships and building hierarchical structures
/// </summary>
public interface IAssetRelationshipService
{
    /// <summary>
    ///     Get all defined relationships
    /// </summary>
    List<AssetRelationship> GetAllRelationships();

    /// <summary>
    ///     Get relationships for a specific asset type
    /// </summary>
    List<AssetRelationship> GetRelationshipsForAsset(string assetType);

    /// <summary>
    ///     Get child asset types for a parent
    /// </summary>
    List<string> GetChildAssetTypes(string parentAssetType);

    /// <summary>
    ///     Get parent asset types for a child
    /// </summary>
    List<string> GetParentAssetTypes(string childAssetType);

    /// <summary>
    ///     Build a hierarchical tree structure of all assets
    /// </summary>
    AssetHierarchyNode BuildAssetHierarchy();

    /// <summary>
    ///     Get the relationship path between two asset types
    /// </summary>
    List<AssetRelationship> GetRelationshipPath(string fromAssetType, string toAssetType);
}

/// <summary>
///     Represents a node in the asset hierarchy tree
/// </summary>
public class AssetHierarchyNode
{
    /// <summary>
    ///     The asset type name
    /// </summary>
    public string AssetType { get; set; } = string.Empty;

    /// <summary>
    ///     Display name for the asset type
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Description of the asset type
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Child nodes in the hierarchy
    /// </summary>
    public List<AssetHierarchyNode> Children { get; set; } = [];

    /// <summary>
    ///     Relationships to child nodes
    /// </summary>
    public List<AssetRelationship> ChildRelationships { get; set; } = [];

    /// <summary>
    ///     UI metadata for this node
    /// </summary>
    public AssetNodeUiMetadata UiMetadata { get; set; } = new();

    /// <summary>
    ///     Whether this node has data available
    /// </summary>
    public bool HasData { get; set; } = true;

    /// <summary>
    ///     Estimated count of items in this asset type
    /// </summary>
    public long? ItemCount { get; set; }
}

/// <summary>
///     UI metadata for asset hierarchy nodes
/// </summary>
public class AssetNodeUiMetadata
{
    /// <summary>
    ///     Icon to display for this asset type
    /// </summary>
    public string Icon { get; set; } = "fas fa-music";

    /// <summary>
    ///     Color theme for this asset type
    /// </summary>
    public string Color { get; set; } = "primary";

    /// <summary>
    ///     Whether this node should be expanded by default
    /// </summary>
    public bool ExpandedByDefault { get; set; } = false;

    /// <summary>
    ///     Display order within siblings
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    ///     Whether this asset type supports search
    /// </summary>
    public bool SupportsSearch { get; set; } = true;

    /// <summary>
    ///     Whether this asset type supports filtering
    /// </summary>
    public bool SupportsFiltering { get; set; } = true;

    /// <summary>
    ///     Available filter types for this asset
    /// </summary>
    public List<string> AvailableFilters { get; set; } = [];

    /// <summary>
    ///     Available sort options for this asset
    /// </summary>
    public List<string> AvailableSortOptions { get; set; } = [];
}

/// <summary>
///     Response model for asset relationship queries
/// </summary>
public class AssetRelationshipResponse
{
    /// <summary>
    ///     The asset relationships
    /// </summary>
    public List<AssetRelationship> Relationships { get; set; } = [];

    /// <summary>
    ///     Hierarchical structure of assets
    /// </summary>
    public AssetHierarchyNode? Hierarchy { get; set; }

    /// <summary>
    ///     Metadata about the relationship query
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
