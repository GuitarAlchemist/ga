// Loads the tiles around a place and hands them to the window.
//
//   dotnet run --project Experiments/Brutal/GeoTiles/GeoTiles -- [--lat 45.4236] [--lon -75.7009] [--height 40]
//       [--radius 400] [--sse 16] [--max-tiles 120] [--seconds 20] [--synthetic] [--no-window]
//
// With GOOGLE_MAPS_API_KEY set, the tiles are Google's Photorealistic 3D Tiles (Map Tiles API), streamed into
// memory for this run only: nothing is written to disk, as Google's terms require. Without the key, or with
// --synthetic, a made-up tileset is used, so the whole path runs with no account and no network.

using System.Globalization;
using GeoTiles;
using StbImageSharp;
using SN = System.Numerics;

internal static class Demo
{
    public static async Task<int> Main(string[] args)
    {
        double Number(string name, double fallback)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length &&
                   double.TryParse(args[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : fallback;
        }

        // Parliament Hill, Ottawa. The height is above the WGS84 ellipsoid, which there is about 34 m above sea level.
        double lat = Number("--lat", 45.4236), lon = Number("--lon", -75.7009), height = Number("--height", 40);
        var radius = Number("--radius", 400);
        var key = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        var synthetic = args.Contains("--synthetic") || string.IsNullOrWhiteSpace(key);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        GoogleTileSource? google = synthetic ? null : new GoogleTileSource(http, key!);
        ITileSource source = google is null ? new SyntheticTileSource(lat, lon, height) : google;
        Console.WriteLine(google is null
            ? "GeoTiles source: synthetic tileset" + (string.IsNullOrWhiteSpace(key) ? " (GOOGLE_MAPS_API_KEY is not set)" : "")
            : "GeoTiles source: Google Photorealistic 3D Tiles, in memory only");

        var frame = new EnuFrame(lat, lon, height);
        var options = new SelectionOptions
        {
            // The eye of the first frame: up and to the side of the place, at the orbit's distance.
            Camera = frame.ToEcef(new Vec3d(radius, radius, radius)),
            Target = frame.Origin,
            Radius = radius,
            MaxScreenSpaceError = Number("--sse", 16),
            MaxTiles = (int)Number("--max-tiles", 120),
        };

        Scene scene;
        try
        {
            scene = await SceneLoader.LoadAsync(source, frame, options, Decode, m => Console.WriteLine($"GeoTiles {m}"));
        }
        catch (Exception e) when (e is HttpRequestException or FormatException or TaskCanceledException)
        {
            Console.Error.WriteLine($"GeoTiles FAIL: {e.Message}");
            return 2;
        }

        Console.WriteLine($"GeoTiles loaded {scene.Tiles} tiles, {scene.Vertices / 3} triangles, in {scene.Requests} requests");
        if (scene.Vertices == 0)
        {
            Console.Error.WriteLine("GeoTiles FAIL: no geometry around this place");
            return 3;
        }

        // Google's terms: the data providers named by the tiles on screen are shown with them, with Google's name.
        var attribution = string.Join("; ", google is null ? scene.Attributions : scene.Attributions.Prepend("Google"));
        Console.WriteLine($"GeoTiles attribution: {attribution}");

        float minY = float.MaxValue, maxY = float.MinValue;
        var vertices = new Vertex[scene.Vertices];
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = new SN.Vector3(scene.Positions[i * 3], scene.Positions[i * 3 + 1], scene.Positions[i * 3 + 2]);
            vertices[i] = new Vertex(p,
                new SN.Vector3(scene.Normals[i * 3], scene.Normals[i * 3 + 1], scene.Normals[i * 3 + 2]),
                new SN.Vector3(scene.Colors[i * 3], scene.Colors[i * 3 + 1], scene.Colors[i * 3 + 2]));
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }

        Console.WriteLine($"GeoTiles local heights {minY:F1} to {maxY:F1} m around the origin");
        if (args.Contains("--no-window"))
        {
            return 0;
        }

        var seconds = Number("--seconds", 20).ToString(CultureInfo.CurrentCulture);
        return TileView.Run([seconds, "--vsync"], vertices, $"GeoTiles (BRUTAL): {attribution}", (minY + maxY) / 2,
            (float)(radius * 1.6));
    }

    private static Pixels? Decode(byte[] image)
    {
        try
        {
            var decoded = ImageResult.FromMemory(image, ColorComponents.RedGreenBlue);
            return new Pixels(decoded.Width, decoded.Height, 3, decoded.Data);
        }
        catch (Exception)
        {
            return null; // a format StbImage does not read (KTX2, WebP): the tile is drawn grey
        }
    }
}
