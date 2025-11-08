namespace GA.Business.Assets.Assets;

using JetBrains.Annotations;

/// <summary>
///     Axis-aligned bounding box for 3D assets
/// </summary>
[PublicAPI]
public sealed record BoundingBox
{
    /// <summary>
    ///     Minimum point (x, y, z)
    /// </summary>
    public Vector3 Min { get; init; } = new();

    /// <summary>
    ///     Maximum point (x, y, z)
    /// </summary>
    public Vector3 Max { get; init; } = new();

    /// <summary>
    ///     Center point of the bounding box
    /// </summary>
    public Vector3 Center => new(
        (Min.X + Max.X) / 2,
        (Min.Y + Max.Y) / 2,
        (Min.Z + Max.Z) / 2
    );

    /// <summary>
    ///     Size of the bounding box (width, height, depth)
    /// </summary>
    public Vector3 Size => new(
        Max.X - Min.X,
        Max.Y - Min.Y,
        Max.Z - Min.Z
    );

    /// <summary>
    ///     Volume of the bounding box
    /// </summary>
    public float Volume => Size.X * Size.Y * Size.Z;
}

/// <summary>
///     3D vector for positions and sizes
/// </summary>
[PublicAPI]
public sealed record Vector3
{
    public Vector3()
    {
    }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}
