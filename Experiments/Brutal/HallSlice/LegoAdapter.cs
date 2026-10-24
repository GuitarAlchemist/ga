using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using LegoScene = Lego.Scene;
using SN = System.Numerics;

// Lego in HallSlice (GuitarAlchemist/gaia, "fractal Lego"): the adapter that materializes a Lego scene as the slice's
// flat-shaded boxes, with the hall's Box, vertex buffer and pipeline. Lego generates and validates; this only turns a
// validated scene's boxes into vertices. A refused scene never gets here: Validated prints why and returns null.
internal static class LegoAdapter
{
    // spec ("temple", "sanctuary@1") with arguments name=value and the seed, generated and validated, or null once the
    // reasons it was refused are printed.
    public static LegoScene? Validated(string spec, ulong seed, IEnumerable<string> args)
    {
        var described = Lego.Api.Describe(spec, seed, args);
        if (described.Valid)
        {
            return described.Scene;
        }

        foreach (var problem in described.Problems)
        {
            Console.Error.WriteLine($"LEGO refused: {problem.Message}");
        }

        return null;
    }

    // The scene's boxes as the slice's vertices, 36 a box, on a dark ground a little wider than its bounds, as the hall
    // stands on its floor. The ground is the host's, not the scene's.
    public static Vertex[] Vertices(LegoScene scene)
    {
        var vertices = new List<Vertex>((scene.Boxes.Count + 1) * 36);
        var size = scene.Max - scene.Min;
        var middle = (scene.Min + scene.Max) / 2f;
        HallSlice.Box(vertices, SN.Matrix4x4.CreateTranslation(middle.X, scene.Min.Y - 0.11f, middle.Z), new SN.Vector3(1.15f * size.X, 0.1f, 1.15f * size.Z), new SN.Vector3(0.16f, 0.15f, 0.14f));
        foreach (var box in scene.Boxes)
        {
            // The slice has no surfaces: a glow is only drawn brighter.
            HallSlice.Box(vertices, box.World, box.Size, box.Surface.Code == 3 ? SN.Vector3.Min(SN.Vector3.One, box.Color * 1.6f) : box.Color);
        }

        return [.. vertices];
    }

    // How far back the orbiting camera must stand, aimed at target and looking down at pitch, for the scene's bounds to
    // stay in frame at every yaw: the bounds' eight corners, projected, within 90 % of the picture.
    public static float Distance(LegoScene scene, SN.Vector3 target, float pitch, float aspect)
    {
        var projection = SN.Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspect, 0.1f, 1e5f);
        var radius = Math.Max(SN.Vector3.Distance(scene.Min, scene.Max) / 2f, 1f);
        for (var distance = radius / 2f; ; distance *= 1.04f)
        {
            var fits = true;
            for (var k = 0; k < 16 && fits; k++)
            {
                var yaw = MathF.Tau * k / 16f;
                var eye = target + (distance * new SN.Vector3(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw)));
                var viewProj = SN.Matrix4x4.CreateLookAt(eye, target, SN.Vector3.UnitY) * projection;
                for (var c = 0; c < 8 && fits; c++)
                {
                    var corner = new SN.Vector3((c & 1) == 0 ? scene.Min.X : scene.Max.X, (c & 2) == 0 ? scene.Min.Y : scene.Max.Y, (c & 4) == 0 ? scene.Min.Z : scene.Max.Z);
                    var p = SN.Vector4.Transform(new SN.Vector4(corner, 1f), viewProj);
                    fits = p.W > 0f && MathF.Abs(p.X / p.W) <= 0.9f && MathF.Abs(p.Y / p.W) <= 0.9f;
                }
            }

            if (fits || distance > 100f * radius)
            {
                return distance;
            }
        }
    }

    // Writes the swapchain's BGRA pixels as an 8-bit RGB PNG, every row unfiltered (Colossus's writer).
    public static void SavePng(string path, ReadOnlySpan<byte> bgra, int width, int height)
    {
        var raw = new byte[height * ((width * 3) + 1)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 4, o = (y * ((width * 3) + 1)) + 1 + (x * 3);
                raw[o] = bgra[i + 2];
                raw[o + 1] = bgra[i + 1];
                raw[o + 2] = bgra[i];
            }
        }

        var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);

        static void Chunk(Stream s, string type, ReadOnlySpan<byte> data)
        {
            Span<byte> word = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
            s.Write(word);
            var body = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type, body);
            data.CopyTo(body.AsSpan(4));
            s.Write(body);
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
            s.Write(word);
        }
    }
}
