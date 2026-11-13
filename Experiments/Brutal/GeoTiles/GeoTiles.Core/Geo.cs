namespace GeoTiles;

/// <summary>A point or a direction in metres, in doubles: ECEF coordinates are about 6.4e6, where a float steps by
/// half a metre.</summary>
public readonly record struct Vec3d(double X, double Y, double Z)
{
    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3d operator *(Vec3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public double Dot(Vec3d b) => X * b.X + Y * b.Y + Z * b.Z;
    public double Length => Math.Sqrt(Dot(this));
    public Vec3d Normalized => this * (1.0 / Length);
}

/// <summary>A 4x4 matrix in doubles, column-major, as glTF and 3D Tiles write it.</summary>
public readonly struct Mat4d
{
    private readonly double[] _m;

    public Mat4d(double[] columnMajor)
    {
        if (columnMajor.Length != 16)
        {
            throw new ArgumentException("a matrix has 16 numbers", nameof(columnMajor));
        }

        _m = columnMajor;
    }

    public static Mat4d Identity { get; } = new([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

    /// <summary>glTF is y-up, 3D Tiles is z-up: a tile's glTF content is turned by +90° about x before the tile's
    /// transform (3D Tiles 1.0, "glTF axes").</summary>
    public static Mat4d YUpToZUp { get; } = new([1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1]);

    public double this[int row, int col] => (_m ?? Identity._m)[col * 4 + row];

    public static Mat4d operator *(Mat4d a, Mat4d b)
    {
        var r = new double[16];
        for (var c = 0; c < 4; c++)
        {
            for (var row = 0; row < 4; row++)
            {
                double s = 0;
                for (var k = 0; k < 4; k++)
                {
                    s += a[row, k] * b[k, c];
                }

                r[c * 4 + row] = s;
            }
        }

        return new Mat4d(r);
    }

    public Vec3d TransformPoint(Vec3d p) => new(
        this[0, 0] * p.X + this[0, 1] * p.Y + this[0, 2] * p.Z + this[0, 3],
        this[1, 0] * p.X + this[1, 1] * p.Y + this[1, 2] * p.Z + this[1, 3],
        this[2, 0] * p.X + this[2, 1] * p.Y + this[2, 2] * p.Z + this[2, 3]);

    /// <summary>The largest stretch the matrix applies along an axis, to scale a radius.</summary>
    public double MaxScale
    {
        get
        {
            double max = 0;
            for (var c = 0; c < 3; c++)
            {
                max = Math.Max(max, new Vec3d(this[0, c], this[1, c], this[2, c]).Length);
            }

            return max;
        }
    }

    public static Mat4d FromTrs(double[]? t, double[]? q, double[]? s)
    {
        double x = q?[0] ?? 0, y = q?[1] ?? 0, z = q?[2] ?? 0, w = q?[3] ?? 1;
        double sx = s?[0] ?? 1, sy = s?[1] ?? 1, sz = s?[2] ?? 1;
        return new Mat4d([
            (1 - 2 * (y * y + z * z)) * sx, 2 * (x * y + z * w) * sx, 2 * (x * z - y * w) * sx, 0,
            2 * (x * y - z * w) * sy, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z + x * w) * sy, 0,
            2 * (x * z + y * w) * sz, 2 * (y * z - x * w) * sz, (1 - 2 * (x * x + y * y)) * sz, 0,
            t?[0] ?? 0, t?[1] ?? 0, t?[2] ?? 0, 1,
        ]);
    }
}

/// <summary>WGS84, and the local frame that brings ECEF metres near zero before anything becomes a float.</summary>
public static class Wgs84
{
    public const double A = 6378137.0;
    public const double F = 1.0 / 298.257223563;
    public const double E2 = F * (2 - F);

    public static Vec3d ToEcef(double latDeg, double lonDeg, double height)
    {
        double lat = latDeg * Math.PI / 180, lon = lonDeg * Math.PI / 180;
        var n = A / Math.Sqrt(1 - E2 * Math.Sin(lat) * Math.Sin(lat));
        return new Vec3d(
            (n + height) * Math.Cos(lat) * Math.Cos(lon),
            (n + height) * Math.Cos(lat) * Math.Sin(lon),
            (n * (1 - E2) + height) * Math.Sin(lat));
    }
}

/// <summary>East, north, up at a point of the ellipsoid. <see cref="ToLocal"/> subtracts in doubles, so the result
/// is small and safe to hand to the GPU as floats.</summary>
public sealed class EnuFrame
{
    public EnuFrame(double latDeg, double lonDeg, double height)
    {
        double lat = latDeg * Math.PI / 180, lon = lonDeg * Math.PI / 180;
        Origin = Wgs84.ToEcef(latDeg, lonDeg, height);
        East = new Vec3d(-Math.Sin(lon), Math.Cos(lon), 0);
        North = new Vec3d(-Math.Sin(lat) * Math.Cos(lon), -Math.Sin(lat) * Math.Sin(lon), Math.Cos(lat));
        Up = new Vec3d(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
    }

    public Vec3d Origin { get; }
    public Vec3d East { get; }
    public Vec3d North { get; }
    public Vec3d Up { get; }

    /// <summary>ECEF to (east, north, up) metres from the origin.</summary>
    public Vec3d ToLocal(Vec3d ecef)
    {
        var d = ecef - Origin;
        return new Vec3d(d.Dot(East), d.Dot(North), d.Dot(Up));
    }

    /// <summary>(east, north, up) metres from the origin to ECEF.</summary>
    public Vec3d ToEcef(Vec3d local) => Origin + East * local.X + North * local.Y + Up * local.Z;
}
