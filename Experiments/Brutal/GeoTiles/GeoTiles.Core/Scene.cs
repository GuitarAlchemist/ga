namespace GeoTiles;

/// <summary>An image decoded to 8-bit RGB or RGBA, row by row from the top.</summary>
public sealed record Pixels(int Width, int Height, int Channels, byte[] Data);

/// <summary>What the window draws: a triangle soup in single precision, in a local frame whose origin is the
/// place looked at (x east, y up, z south), with a flat normal and a colour per vertex.</summary>
public sealed record Scene(float[] Positions, float[] Normals, float[] Colors, IReadOnlyList<string> Attributions,
    int Tiles, int Requests, bool Truncated)
{
    public int Vertices => Positions.Length / 3;

    /// <summary>A texture coordinate per vertex, (0, 0) at the top-left of the image; empty when nothing is
    /// textured.</summary>
    public float[] Uvs { get; init; } = [];

    /// <summary>The vertices in runs that share one image, in order and without gaps; empty when nothing is
    /// textured.</summary>
    public IReadOnlyList<Batch> Batches { get; init; } = [];
}

/// <summary><paramref name="Count"/> vertices from <paramref name="First"/>, drawn with <paramref name="Image"/>
/// at their texture coordinates, or with their colours when it is null.</summary>
public sealed record Batch(int First, int Count, Pixels? Image);

public static class SceneLoader
{
    /// <summary>Selects, fetches and decodes the tiles around a place, then brings them from ECEF into the local
    /// frame in doubles. Nothing is written to disk. <paramref name="decodeImage"/> turns a JPEG or PNG into
    /// pixels (null when it cannot); colours are sampled from it at each vertex, a first stand-in for real
    /// texturing.</summary>
    public static async Task<Scene> LoadAsync(ITileSource source, EnuFrame frame, SelectionOptions options,
        Func<byte[], Pixels?> decodeImage, Action<string>? log = null, CancellationToken cancel = default)
    {
        var requests = 1;
        var rootDoc = TilesetParser.Parse(await source.GetAsync(source.Root, cancel), source.Root);
        var attributions = new List<string>();
        Add(attributions, rootDoc.Copyright);

        var selection = await TileSelector.SelectAsync(rootDoc.Root, options, async (uri, parent) =>
        {
            requests++;
            return TilesetParser.Parse(await source.GetAsync(uri, cancel), uri, parent.Transform, parent.Replace);
        });
        foreach (var c in selection.Copyrights)
        {
            Add(attributions, c);
        }

        log?.Invoke($"selected {selection.Tiles.Count} tiles through {selection.TilesetsLoaded} nested tilesets" +
                    (selection.Truncated ? " (stopped at the cap)" : ""));

        var positions = new List<float>();
        var normals = new List<float>();
        var colors = new List<float>();
        var uvs = new List<float>();
        var batches = new List<Batch>();
        foreach (var tile in selection.Tiles)
        {
            requests++;
            var mesh = GlbReader.Read(await source.GetAsync(tile.Content, cancel), tile.Transform);
            Add(attributions, mesh.Copyright);
            foreach (var p in mesh.Primitives)
            {
                var pixels = p.Image is null ? null : decodeImage(p.Image);
                var first = positions.Count / 3;
                // ECEF to local, in doubles; only then floats. Render frame: x east, y up, z south.
                var local = Array.ConvertAll(p.Positions, ecef =>
                {
                    var enu = frame.ToLocal(ecef);
                    return (X: (float)enu.X, Y: (float)enu.Z, Z: (float)-enu.Y);
                });
                for (var i = 0; i + 2 < p.Indices.Length; i += 3)
                {
                    int ia = p.Indices[i], ib = p.Indices[i + 1], ic = p.Indices[i + 2];
                    var (a, b, c) = (local[ia], local[ib], local[ic]);
                    float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    var length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (length < 1e-12f)
                    {
                        continue; // a degenerate triangle
                    }

                    foreach (var index in (int[])[ia, ib, ic])
                    {
                        var v = local[index];
                        positions.AddRange([v.X, v.Y, v.Z]);
                        normals.AddRange([nx / length, ny / length, nz / length]);
                        colors.AddRange(Sample(pixels, p.Uvs, index));
                        uvs.AddRange(p.Uvs is null ? [0f, 0f] : [p.Uvs[index * 2], p.Uvs[index * 2 + 1]]);
                    }
                }

                if (positions.Count / 3 > first)
                {
                    batches.Add(new Batch(first, positions.Count / 3 - first, p.Uvs is null ? null : pixels));
                }
            }
        }

        return new Scene([.. positions], [.. normals], [.. colors], attributions, selection.Tiles.Count, requests,
            selection.Truncated) { Uvs = [.. uvs], Batches = batches };
    }

    /// <summary>Google separates the providers of one tile with semicolons.</summary>
    private static void Add(List<string> into, string? copyright)
    {
        foreach (var part in (copyright ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!into.Contains(part))
            {
                into.Add(part);
            }
        }
    }

    private static float[] Sample(Pixels? pixels, float[]? uvs, int vertex)
    {
        if (pixels is null || uvs is null)
        {
            return [0.62f, 0.62f, 0.6f];
        }

        // glTF: (0, 0) is the top-left corner of the image.
        var x = Math.Clamp((int)(uvs[vertex * 2] * pixels.Width), 0, pixels.Width - 1);
        var y = Math.Clamp((int)(uvs[vertex * 2 + 1] * pixels.Height), 0, pixels.Height - 1);
        var at = (y * pixels.Width + x) * pixels.Channels;
        // The image is sRGB (glTF base colour); the window writes to an sRGB target, which expects linear light.
        static float Linear(byte value) => MathF.Pow(value / 255f, 2.2f);
        return [Linear(pixels.Data[at]), Linear(pixels.Data[at + 1]), Linear(pixels.Data[at + 2])];
    }
}
