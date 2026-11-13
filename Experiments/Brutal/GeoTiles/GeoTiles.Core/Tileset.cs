namespace GeoTiles;

using System.Text.Json;

/// <summary>One node of an OGC 3D Tiles tree, with what the selection needs: a bounding sphere in the tileset's
/// world frame (ECEF for Google), the geometric error, and where its content is.</summary>
public sealed class Tile
{
    public required Vec3d Center { get; init; }
    public required double Radius { get; init; }
    public required double GeometricError { get; init; }

    /// <summary>true: children replace this tile; false: they add to it.</summary>
    public required bool Replace { get; init; }

    public required Mat4d Transform { get; init; }
    public IReadOnlyList<Uri> Contents { get; init; } = [];
    public List<Tile> Children { get; } = [];

    /// <summary>A content that is another tileset (Google nests them deeply) rather than a mesh.</summary>
    public static bool IsTileset(Uri content) =>
        content.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}

public sealed record TilesetDocument(Tile Root, string? Copyright);

public static class TilesetParser
{
    /// <summary>Parses a tileset.json fetched from <paramref name="source"/>. <paramref name="parentTransform"/>
    /// and <paramref name="parentReplace"/> carry the state of the tile that pointed at an external tileset.</summary>
    public static TilesetDocument Parse(ReadOnlySpan<byte> json, Uri source, Mat4d? parentTransform = null,
        bool parentReplace = true)
    {
        using var doc = JsonDocument.Parse(json.ToArray());
        var rootEl = doc.RootElement;
        string? copyright = null;
        if (rootEl.TryGetProperty("asset", out var asset) && asset.TryGetProperty("copyright", out var c))
        {
            copyright = c.GetString();
        }

        if (!rootEl.TryGetProperty("root", out var root))
        {
            throw new FormatException($"{Redact(source)}: a tileset without a root tile");
        }

        return new TilesetDocument(ParseTile(root, source, parentTransform ?? Mat4d.Identity, parentReplace), copyright);
    }

    private static Tile ParseTile(JsonElement el, Uri source, Mat4d parent, bool parentReplace)
    {
        var transform = parent;
        if (el.TryGetProperty("transform", out var t))
        {
            transform = parent * new Mat4d(Doubles(t, 16, "transform"));
        }

        var replace = parentReplace;
        if (el.TryGetProperty("refine", out var r))
        {
            replace = !string.Equals(r.GetString(), "ADD", StringComparison.OrdinalIgnoreCase);
        }

        if (!el.TryGetProperty("boundingVolume", out var bv))
        {
            throw new FormatException("a tile without a boundingVolume");
        }

        var (center, radius) = Sphere(bv, transform);
        var contents = new List<Uri>();
        if (el.TryGetProperty("content", out var content))
        {
            AddContent(content, source, contents);
        }

        if (el.TryGetProperty("contents", out var many))
        {
            foreach (var one in many.EnumerateArray())
            {
                AddContent(one, source, contents);
            }
        }

        var tile = new Tile
        {
            Center = center,
            Radius = radius,
            GeometricError = el.TryGetProperty("geometricError", out var ge) ? ge.GetDouble() : 0,
            Replace = replace,
            Transform = transform,
            Contents = contents,
        };
        if (el.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                tile.Children.Add(ParseTile(child, source, transform, replace));
            }
        }

        return tile;
    }

    private static void AddContent(JsonElement content, Uri source, List<Uri> into)
    {
        // "uri" since 3D Tiles 1.0; "url" in older tilesets.
        if (content.TryGetProperty("uri", out var u) || content.TryGetProperty("url", out u))
        {
            into.Add(new Uri(source, u.GetString()!));
        }
    }

    /// <summary>A sphere around the bounding volume: exact for a sphere, enclosing for a box and a region.</summary>
    private static (Vec3d Center, double Radius) Sphere(JsonElement bv, Mat4d transform)
    {
        if (bv.TryGetProperty("box", out var box))
        {
            var b = Doubles(box, 12, "box");
            double r2 = 0;
            for (var i = 3; i < 12; i++)
            {
                r2 += b[i] * b[i];
            }

            return (transform.TransformPoint(new Vec3d(b[0], b[1], b[2])), Math.Sqrt(r2) * transform.MaxScale);
        }

        if (bv.TryGetProperty("sphere", out var sphere))
        {
            var s = Doubles(sphere, 4, "sphere");
            return (transform.TransformPoint(new Vec3d(s[0], s[1], s[2])), s[3] * transform.MaxScale);
        }

        if (bv.TryGetProperty("region", out var region))
        {
            // West, south, east, north in radians, then the two heights. A region is in WGS84 whatever the tile's
            // transform says.
            var g = Doubles(region, 6, "region");
            const double deg = 180 / Math.PI;
            var center = Wgs84.ToEcef((g[1] + g[3]) / 2 * deg, (g[0] + g[2]) / 2 * deg, (g[4] + g[5]) / 2);
            double radius = 0;
            foreach (var lat in new[] { g[1], g[3] })
            {
                foreach (var lon in new[] { g[0], g[2] })
                {
                    foreach (var h in new[] { g[4], g[5] })
                    {
                        radius = Math.Max(radius, (Wgs84.ToEcef(lat * deg, lon * deg, h) - center).Length);
                    }
                }
            }

            return (center, radius);
        }

        throw new FormatException("a boundingVolume that is not a box, a sphere or a region");
    }

    private static double[] Doubles(JsonElement array, int count, string what)
    {
        if (array.GetArrayLength() != count)
        {
            throw new FormatException($"{what}: {array.GetArrayLength()} numbers, expected {count}");
        }

        var result = new double[count];
        var i = 0;
        foreach (var n in array.EnumerateArray())
        {
            result[i++] = n.GetDouble();
        }

        return result;
    }

    /// <summary>A URL without its query: the query carries the API key and the session.</summary>
    public static string Redact(Uri uri) => uri.GetLeftPart(UriPartial.Path);
}
