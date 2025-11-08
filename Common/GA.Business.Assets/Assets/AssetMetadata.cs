namespace GA.Business.Assets.Assets;

using JetBrains.Annotations;

/// <summary>
///     Metadata for a 3D asset
/// </summary>
[PublicAPI]
public sealed record AssetMetadata
{
    /// <summary>
    ///     Unique identifier for the asset
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Human-readable name of the asset
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Category of the asset
    /// </summary>
    public required AssetCategory Category { get; init; }

    /// <summary>
    ///     Path to the GLB file (relative or GridFS reference)
    /// </summary>
    public required string GlbPath { get; init; }

    /// <summary>
    ///     Number of polygons/triangles in the model
    /// </summary>
    public required int PolyCount { get; init; }

    /// <summary>
    ///     License information (e.g., "CC Attribution", "Free Download")
    /// </summary>
    public required string License { get; init; }

    /// <summary>
    ///     Source URL where the asset was downloaded from
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    ///     Original author/creator of the asset
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    ///     Tags for searching and filtering
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new();

    /// <summary>
    ///     Axis-aligned bounding box
    /// </summary>
    public BoundingBox? Bounds { get; init; }

    /// <summary>
    ///     File size in bytes
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    ///     Whether the asset has been optimized for WebGPU
    /// </summary>
    public bool IsOptimized { get; init; }

    /// <summary>
    ///     Thumbnail image path (optional)
    /// </summary>
    public string? ThumbnailPath { get; init; }

    /// <summary>
    ///     User who imported the asset (optional)
    /// </summary>
    public string? ImportedBy { get; init; }
}
