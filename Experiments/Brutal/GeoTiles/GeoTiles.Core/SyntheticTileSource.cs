namespace GeoTiles;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

/// <summary>A made-up square kilometre of hills and blocks, served as a real 3D Tiles tree: a coarse root, four
/// finer quadrants (one behind an external tileset), Draco geometry, a PNG texture, tile transforms from a local
/// frame to ECEF. It exercises the same path as Google's tiles with no key, no network and no one's data.</summary>
public sealed class SyntheticTileSource : ITileSource
{
    public const string Copyright = "Synthetic terrain (GeoTiles demo)";
    private const double Half = 500;
    private readonly Dictionary<string, byte[]> _files = [];

    public SyntheticTileSource(double latDeg, double lonDeg, double height)
    {
        var frame = new EnuFrame(latDeg, lonDeg, height);
        var o = frame.Origin;
        // Local (east, north, up) to ECEF: the tile transform that real tilesets carry.
        var transform = string.Join(",", new[]
        {
            frame.East.X, frame.East.Y, frame.East.Z, 0, frame.North.X, frame.North.Y, frame.North.Z, 0,
            frame.Up.X, frame.Up.Y, frame.Up.Z, 0, o.X, o.Y, o.Z, 1,
        }.Select(d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        var texture = Texture();
        _files["coarse.glb"] = Terrain(-Half, -Half, Half, Half, 8, false, texture);
        var children = new StringBuilder();
        for (var q = 0; q < 4; q++)
        {
            double e0 = q % 2 == 0 ? -Half : 0, n0 = q < 2 ? -Half : 0;
            _files[$"q{q}.glb"] = Terrain(e0, n0, e0 + Half, n0 + Half, 24, true, texture);
            var box = $"{{\"box\":[{e0 + Half / 2},{n0 + Half / 2},60, {Half / 2},0,0, 0,{Half / 2},0, 0,0,120]}}";
            var content = $"q{q}.glb";
            if (q == 3)
            {
                // The last quadrant sits behind a tileset of its own, as Google's deeper levels do.
                _files["q3.json"] = Encoding.UTF8.GetBytes(
                    $"{{\"asset\":{{\"version\":\"1.0\",\"copyright\":\"{Copyright}\"}},\"geometricError\":0," +
                    $"\"root\":{{\"boundingVolume\":{box},\"geometricError\":0,\"content\":{{\"uri\":\"q3.glb\"}}}}}}");
                content = "q3.json";
            }

            children.Append(q == 0 ? "" : ",").Append(
                $"{{\"boundingVolume\":{box},\"geometricError\":{(q == 3 ? 30 : 0)},\"content\":{{\"uri\":\"{content}\"}}}}");
        }

        _files["tileset.json"] = Encoding.UTF8.GetBytes(
            $"{{\"asset\":{{\"version\":\"1.0\",\"copyright\":\"{Copyright}\"}},\"geometricError\":200," +
            $"\"root\":{{\"transform\":[{transform}],\"refine\":\"REPLACE\",\"geometricError\":30," +
            $"\"boundingVolume\":{{\"box\":[0,0,60, {Half},0,0, 0,{Half},0, 0,0,120]}}," +
            $"\"content\":{{\"uri\":\"coarse.glb\"}},\"children\":[{children}]}}}}");
    }

    public Uri Root { get; } = new("https://synthetic.invalid/tileset.json");

    public Task<byte[]> GetAsync(Uri uri, CancellationToken cancel = default) =>
        _files.TryGetValue(uri.Segments[^1], out var bytes)
            ? Task.FromResult(bytes)
            : throw new FileNotFoundException($"synthetic tileset: no {uri.Segments[^1]}");

    /// <summary>The ground height in metres at (east, north).</summary>
    public static double Ground(double e, double n) =>
        40 + 30 * Math.Sin(e / 160) * Math.Cos(n / 210) + 12 * Math.Sin((e + n) / 55);

    private static byte[] Terrain(double e0, double n0, double e1, double n1, int cells, bool blocks, byte[] texture)
    {
        var positions = new List<float>();
        var uvs = new List<float>();
        var indices = new List<int>();

        void Vertex(double e, double n, double up)
        {
            // glTF is y-up: (east, up, -north), which the reader turns back into (east, north, up).
            positions.AddRange([(float)e, (float)up, (float)-n]);
            uvs.AddRange([(float)((e + Half) / (2 * Half)), (float)((n + Half) / (2 * Half))]);
        }

        for (var j = 0; j <= cells; j++)
        {
            for (var i = 0; i <= cells; i++)
            {
                double e = e0 + (e1 - e0) * i / cells, n = n0 + (n1 - n0) * j / cells;
                Vertex(e, n, Ground(e, n));
            }
        }

        for (var j = 0; j < cells; j++)
        {
            for (var i = 0; i < cells; i++)
            {
                int a = j * (cells + 1) + i, b = a + 1, c = a + cells + 1, d = c + 1;
                indices.AddRange([a, b, d, a, d, c]);
            }
        }

        if (blocks)
        {
            for (var k = 0; k < 6; k++)
            {
                double e = e0 + (e1 - e0) * (0.15 + 0.14 * k), n = n0 + (n1 - n0) * (0.2 + 0.11 * ((k * 5) % 7));
                double w = 14 + 3 * (k % 3), h = 25 + 18 * (k % 4), g = Ground(e, n) - 2;
                var first = positions.Count / 3;
                foreach (var up in new[] { g, g + h })
                {
                    Vertex(e - w, n - w, up);
                    Vertex(e + w, n - w, up);
                    Vertex(e + w, n + w, up);
                    Vertex(e - w, n + w, up);
                }

                foreach (var (a, b, c, d) in new[] { (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7) })
                {
                    indices.AddRange([first + a, first + b, first + c, first + a, first + c, first + d]);
                }
            }
        }

        return GlbWriter.Write([.. positions], [.. uvs], [.. indices], draco: true, copyright: Copyright,
            image: texture, imageMime: "image/png");
    }

    /// <summary>A 64x64 PNG: green fields cut by a grey grid of "streets".</summary>
    private static byte[] Texture()
    {
        const int size = 64;
        var raw = new byte[size * (1 + size * 3)];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var street = x % 8 == 0 || y % 8 == 0;
                var shade = (byte)(20 * ((x / 8 + y / 8) % 3));
                var at = y * (1 + size * 3) + 1 + x * 3;
                raw[at] = street ? (byte)120 : (byte)(70 + shade);
                raw[at + 1] = street ? (byte)120 : (byte)(130 + shade);
                raw[at + 2] = street ? (byte)125 : (byte)(60 + shade / 2);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, size);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8; // bits per channel
        header[9] = 2; // RGB
        Chunk(png, "IHDR", header);
        using (var packed = new MemoryStream())
        {
            using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            {
                z.Write(raw);
            }

            Chunk(png, "IDAT", packed.ToArray());
        }

        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        png.Write(word);
        var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        png.Write(body);
        var crc = 0xFFFFFFFFu;
        foreach (var b in body)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
        png.Write(word);
    }
}
