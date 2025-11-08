namespace GA.Data.MongoDB.Models;

using Business.Assets.Assets;

/// <summary>
///     MongoDB document for 3D assets
/// </summary>
[PublicAPI]
public sealed record AssetDocument : DocumentBase
{
    /// <summary>
    ///     Human-readable name of the asset
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Category of the asset
    /// </summary>
    public required AssetCategory Category { get; init; }

    /// <summary>
    ///     GridFS file ID for the GLB data
    /// </summary>
    public ObjectId? GlbFileId { get; init; }

    /// <summary>
    ///     Path to the GLB file (if stored externally)
    /// </summary>
    public string? GlbPath { get; init; }

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
    ///     Axis-aligned bounding box (min/max coordinates)
    /// </summary>
    public BoundingBoxData? Bounds { get; init; }

    /// <summary>
    ///     File size in bytes
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    ///     Whether the asset has been optimized for WebGPU
    /// </summary>
    public bool IsOptimized { get; init; }

    /// <summary>
    ///     GridFS file ID for the thumbnail image
    /// </summary>
    public ObjectId? ThumbnailFileId { get; init; }

    /// <summary>
    ///     User who imported the asset
    /// </summary>
    public string? ImportedBy { get; init; }
}

/// <summary>
///     Bounding box data for MongoDB storage
/// </summary>
[PublicAPI]
public sealed record BoundingBoxData
{
    public required Vector3Data Min { get; init; }
    public required Vector3Data Max { get; init; }

    public static BoundingBoxData FromBoundingBox(BoundingBox box)
    {
        return new BoundingBoxData
        {
            Min = Vector3Data.FromVector3(box.Min),
            Max = Vector3Data.FromVector3(box.Max)
        };
    }

    public BoundingBox ToBoundingBox()
    {
        return new BoundingBox
        {
            Min = Min.ToVector3(),
            Max = Max.ToVector3()
        };
    }
}

/// <summary>
///     3D vector data for MongoDB storage
/// </summary>
[PublicAPI]
public sealed record Vector3Data
{
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Z { get; init; }

    public static Vector3Data FromVector3(Vector3 v)
    {
        return new Vector3Data
        {
            X = v.X,
            Y = v.Y,
            Z = v.Z
        };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}
