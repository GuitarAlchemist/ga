namespace GeoTiles;

using System.Globalization;
using System.Text.Json;

/// <summary>Buildings of OpenStreetMap, extruded: open data (ODbL), which may be stored and redistributed with
/// its attribution, unlike the tiles of Google. The footprints come from an Overpass answer
/// (<c>out geom</c>); the heights from the <c>height</c> or <c>building:levels</c> tags, and a guess without them.</summary>
public static class OsmBuildings
{
    public const string Attribution = "© OpenStreetMap contributors";

    /// <summary>The height given to a building that states none, and the height of one level, in metres.</summary>
    public const double DefaultHeight = 8, LevelHeight = 3;

    /// <summary>The Overpass query for the buildings within <paramref name="radius"/> metres of a place.</summary>
    public static string Query(double lat, double lon, double radius)
    {
        var around = string.Create(CultureInfo.InvariantCulture, $"(around:{radius:F0},{lat:F6},{lon:F6})");
        return $"[out:json][timeout:25];(way[\"building\"]{around};way[\"building:part\"]{around};" +
               $"relation[\"building\"]{around};);out geom;";
    }

    /// <summary>The scene of an Overpass answer, in the render frame of <paramref name="frame"/> (x east, y up,
    /// z south), on a ground at the frame's own height. Rings that are not closed are skipped and counted in
    /// <paramref name="skipped"/>.</summary>
    public static Scene Parse(string json, EnuFrame frame, double groundHeight, out int skipped)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var colors = new List<float>();
        var buildings = 0;
        skipped = 0;

        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            var (bottom, top) = Heights(element);
            var rings = new List<JsonElement>();
            if (element.TryGetProperty("geometry", out var geometry))
            {
                rings.Add(geometry);
            }
            else if (element.TryGetProperty("members", out var members))
            {
                foreach (var member in members.EnumerateArray())
                {
                    if (member.TryGetProperty("role", out var role) && role.GetString() == "outer" &&
                        member.TryGetProperty("geometry", out var outer))
                    {
                        rings.Add(outer);
                    }
                }
            }

            foreach (var ring in rings)
            {
                var points = new List<(double E, double N)>();
                foreach (var node in ring.EnumerateArray())
                {
                    var local = frame.ToLocal(Wgs84.ToEcef(node.GetProperty("lat").GetDouble(),
                        node.GetProperty("lon").GetDouble(), groundHeight));
                    points.Add((local.X, local.Y));
                }

                if (points.Count < 4 || Distance(points[0], points[^1]) > 0.01)
                {
                    skipped++;
                    continue;
                }

                points.RemoveAt(points.Count - 1);
                if (Area(points) < 0)
                {
                    points.Reverse(); // counter-clockwise seen from above
                }

                Extrude(points, bottom, top, positions, normals, colors);
                buildings++;
            }
        }

        return new Scene([.. positions], [.. normals], [.. colors], [Attribution], buildings, 1, false);
    }

    private static (double Bottom, double Top) Heights(JsonElement element)
    {
        double? Tag(string name)
        {
            if (!element.TryGetProperty("tags", out var tags) || !tags.TryGetProperty(name, out var value))
            {
                return null;
            }

            // "12", "12.5 m": the leading number.
            var text = (value.GetString() ?? "").Trim();
            var end = 0;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
            {
                end++;
            }

            return double.TryParse(text.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;
        }

        var top = Tag("height") ?? (Tag("building:levels") is { } levels ? levels * LevelHeight : DefaultHeight);
        var bottom = Tag("min_height") ?? 0;
        return top > bottom ? (bottom, top) : (0, DefaultHeight);
    }

    private static void Extrude(List<(double E, double N)> ring, double bottom, double top, List<float> positions,
        List<float> normals, List<float> colors)
    {
        void Triangle((double E, double N, double U) a, (double E, double N, double U) b, (double E, double N, double U) c,
            (double E, double N, double U) normal, float[] colour)
        {
            foreach (var p in ((double E, double N, double U)[])[a, b, c])
            {
                positions.AddRange([(float)p.E, (float)p.U, (float)-p.N]);
                normals.AddRange([(float)normal.E, (float)normal.U, (float)-normal.N]);
                colors.AddRange(colour);
            }
        }

        float[] wall = [0.78f, 0.72f, 0.62f], roof = [0.45f, 0.47f, 0.5f];
        for (var i = 0; i < ring.Count; i++)
        {
            var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
            double de = q.E - p.E, dn = q.N - p.N, length = Math.Sqrt(de * de + dn * dn);
            if (length < 1e-6)
            {
                continue;
            }

            // The ring turns counter-clockwise: the outside is to the right of each edge.
            var outward = (dn / length, -de / length, 0.0);
            Triangle((p.E, p.N, bottom), (q.E, q.N, bottom), (q.E, q.N, top), outward, wall);
            Triangle((p.E, p.N, bottom), (q.E, q.N, top), (p.E, p.N, top), outward, wall);
        }

        foreach (var (a, b, c) in Triangulate(ring))
        {
            Triangle((ring[a].E, ring[a].N, top), (ring[b].E, ring[b].N, top), (ring[c].E, ring[c].N, top), (0, 0, 1), roof);
        }
    }

    /// <summary>Ear clipping of a counter-clockwise simple polygon: n - 2 triangles, as indices into it.</summary>
    public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<(double E, double N)> ring)
    {
        var triangles = new List<(int, int, int)>();
        var left = Enumerable.Range(0, ring.Count).ToList();
        var guard = ring.Count * ring.Count;
        while (left.Count > 3 && guard-- > 0)
        {
            var clipped = false;
            for (var i = 0; i < left.Count; i++)
            {
                int a = left[(i + left.Count - 1) % left.Count], b = left[i], c = left[(i + 1) % left.Count];
                if (Cross(ring[a], ring[b], ring[c]) <= 0)
                {
                    continue; // a reflex corner
                }

                var empty = true;
                foreach (var other in left)
                {
                    if (other != a && other != b && other != c && Inside(ring[other], ring[a], ring[b], ring[c]))
                    {
                        empty = false;
                        break;
                    }
                }

                if (empty)
                {
                    triangles.Add((a, b, c));
                    left.RemoveAt(i);
                    clipped = true;
                    break;
                }
            }

            if (!clipped)
            {
                break; // a ring that crosses itself: what is left is dropped
            }
        }

        if (left.Count == 3)
        {
            triangles.Add((left[0], left[1], left[2]));
        }

        return triangles;
    }

    private static double Cross((double E, double N) a, (double E, double N) b, (double E, double N) c) =>
        (b.E - a.E) * (c.N - a.N) - (b.N - a.N) * (c.E - a.E);

    private static bool Inside((double E, double N) p, (double E, double N) a, (double E, double N) b, (double E, double N) c) =>
        Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0;

    private static double Area(List<(double E, double N)> ring)
    {
        var twice = 0.0;
        for (var i = 0; i < ring.Count; i++)
        {
            var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
            twice += p.E * q.N - q.E * p.N;
        }

        return twice / 2;
    }

    private static double Distance((double E, double N) a, (double E, double N) b) =>
        Math.Sqrt((a.E - b.E) * (a.E - b.E) + (a.N - b.N) * (a.N - b.N));
}
