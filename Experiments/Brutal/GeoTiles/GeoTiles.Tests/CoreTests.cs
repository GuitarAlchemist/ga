namespace GeoTiles.Tests;

using System.Text;
using Xunit;

public class GeodesyTests
{
    [Fact]
    public void Ecef_of_the_equator_and_of_the_pole_are_the_ellipsoid_axes()
    {
        var equator = Wgs84.ToEcef(0, 0, 0);
        Assert.Equal(6378137.0, equator.X, 6);
        Assert.Equal(0, equator.Y, 6);
        Assert.Equal(0, equator.Z, 6);

        // The polar radius of WGS84, from its definition: b = a(1 - f) = 6 356 752.314 245 m.
        var pole = Wgs84.ToEcef(90, 0, 0);
        Assert.Equal(6356752.314245, pole.Z, 4);
        Assert.Equal(0, pole.X, 6);

        var east = Wgs84.ToEcef(0, 90, 100);
        Assert.Equal(6378237.0, east.Y, 6);
    }

    [Fact]
    public void Enu_puts_the_origin_at_zero_and_names_east_north_and_up()
    {
        // Parliament Hill, Ottawa.
        var frame = new EnuFrame(45.4236, -75.7009, 70);
        var origin = frame.ToLocal(frame.Origin);
        Assert.Equal(0, origin.Length, 9);

        var above = frame.ToLocal(Wgs84.ToEcef(45.4236, -75.7009, 170));
        Assert.Equal(0, above.X, 6);
        Assert.Equal(0, above.Y, 6);
        Assert.Equal(100, above.Z, 6);

        var north = frame.ToLocal(Wgs84.ToEcef(45.4246, -75.7009, 70));
        Assert.InRange(north.Y, 110, 112); // a thousandth of a degree of latitude is about 111 m
        Assert.Equal(0, north.X, 3);

        var eastward = frame.ToLocal(Wgs84.ToEcef(45.4236, -75.6999, 70));
        Assert.InRange(eastward.X, 77, 79); // 111.3 m x cos(45.42°)
        Assert.Equal(0, eastward.Y, 1);

        var back = frame.ToEcef(new Vec3d(12.5, -40.25, 3));
        Assert.Equal(new Vec3d(12.5, -40.25, 3).X, frame.ToLocal(back).X, 6);
    }

    [Fact]
    public void A_float_keeps_millimetres_in_enu_and_loses_decimetres_in_ecef()
    {
        // Why the subtraction is done in doubles: two points 1 cm apart.
        var frame = new EnuFrame(45.4236, -75.7009, 70);
        var a = frame.ToEcef(new Vec3d(100, 200, 30));
        var b = frame.ToEcef(new Vec3d(100.01, 200, 30));

        var ecefAsFloats = Math.Abs((float)b.Y - (float)a.Y) + Math.Abs((float)b.X - (float)a.X) +
                           Math.Abs((float)b.Z - (float)a.Z);
        Assert.True(ecefAsFloats is 0 or >= 0.25f, $"a float near 4.4e6 steps by 0.5 m, got {ecefAsFloats}");

        var local = (float)frame.ToLocal(b).X - (float)frame.ToLocal(a).X;
        Assert.InRange(local, 0.0099f, 0.0101f);
    }
}

public class TilesetTests
{
    private static readonly Uri Base = new("https://tile.googleapis.com/v1/3dtiles/root.json");

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>A box of half-size <paramref name="half"/> centred at (x, 0, 0).</summary>
    private static string Box(double x, double half) => $"{{\"box\":[{x},0,0, {half},0,0, 0,{half},0, 0,0,{half}]}}";

    private const string Root = """
        {
          "asset": { "version": "1.0", "copyright": "Root provider" },
          "root": {
            "boundingVolume": BOXROOT, "geometricError": 400, "refine": "REPLACE",
            "content": { "uri": "coarse.glb" },
            "children": [
              { "boundingVolume": BOXNEAR, "geometricError": 40, "content": { "uri": "/v1/3dtiles/near.json?session=S1" } },
              { "boundingVolume": BOXFAR, "geometricError": 40, "content": { "uri": "far.glb" } }
            ]
          }
        }
        """;

    private const string Near = """
        {
          "asset": { "version": "1.0", "copyright": "Near provider" },
          "root": {
            "boundingVolume": BOXNEAR, "geometricError": 40,
            "content": { "uri": "near-coarse.glb" },
            "children": [
              { "boundingVolume": BOXNEAR, "geometricError": 0, "content": { "uri": "near-fine.glb" } }
            ]
          }
        }
        """;

    private static TilesetDocument Document(string json, Uri source, Mat4d? transform = null) =>
        TilesetParser.Parse(Json(json.Replace("BOXROOT", Box(0, 2000)).Replace("BOXNEAR", Box(-500, 100))
            .Replace("BOXFAR", Box(1500, 100))), source, transform);

    private static SelectionOptions Options(Vec3d camera, double radius = 5000, int maxTiles = 50) => new()
    {
        Camera = camera, Target = new Vec3d(0, 0, 0), Radius = radius, ScreenHeight = 720, MaxTiles = maxTiles,
    };

    private static Task<SelectionResult> Select(SelectionOptions options, List<Uri>? loaded = null) =>
        TileSelector.SelectAsync(Document(Root, Base).Root, options, (uri, tile) =>
        {
            loaded?.Add(uri);
            return Task.FromResult(Document(Near, uri, tile.Transform));
        });

    [Fact]
    public void Screen_space_error_is_the_geometric_error_in_pixels()
    {
        // 10 m of error seen from 1000 m with a 90° field of view on 1000 pixels: tan(45°) = 1, so 10 * 1000 / 2000.
        Assert.Equal(5, TileSelector.ScreenSpaceError(10, 1000, 1000, Math.PI / 2), 9);
        Assert.Equal(10, TileSelector.ScreenSpaceError(10, 500, 1000, Math.PI / 2), 9);
    }

    [Fact]
    public async Task From_far_away_the_coarse_root_is_enough()
    {
        var result = await Select(Options(new Vec3d(0, 0, 100_000)));
        Assert.Equal(["coarse.glb"], result.Tiles.Select(t => t.Content.Segments[^1]));
        Assert.Equal(0, result.TilesetsLoaded);
    }

    [Fact]
    public async Task Up_close_children_replace_their_parent_and_only_where_it_matters()
    {
        // Next to the near tile, 2 km from the far one.
        var loaded = new List<Uri>();
        var result = await Select(Options(new Vec3d(-500, 0, 150)), loaded);
        Assert.Equal(["near-fine.glb", "far.glb"], result.Tiles.Select(t => t.Content.Segments[^1]));
        Assert.Single(loaded);
        Assert.Equal(["Near provider"], result.Copyrights);
    }

    [Fact]
    public async Task Tiles_outside_the_place_of_interest_are_not_fetched()
    {
        var result = await Select(Options(new Vec3d(-500, 0, 150), radius: 700));
        Assert.Equal(["near-fine.glb"], result.Tiles.Select(t => t.Content.Segments[^1]));
    }

    [Fact]
    public async Task The_cap_on_tiles_stops_the_walk_and_says_so()
    {
        var result = await Select(Options(new Vec3d(-500, 0, 150), maxTiles: 1));
        Assert.Single(result.Tiles);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void An_add_tile_keeps_its_own_content_and_a_transform_moves_the_volume()
    {
        var doc = TilesetParser.Parse(Json("""
            { "asset": { "version": "1.0" },
              "root": { "boundingVolume": { "sphere": [1, 2, 3, 10] }, "geometricError": 5, "refine": "ADD",
                        "transform": [2,0,0,0, 0,2,0,0, 0,0,2,0, 100,0,0,1],
                        "children": [ { "boundingVolume": { "sphere": [0, 0, 0, 1] }, "geometricError": 0 } ] } }
            """), Base);
        Assert.False(doc.Root.Replace);
        Assert.False(doc.Root.Children[0].Replace); // refine is inherited
        Assert.Equal(new Vec3d(102, 4, 6), doc.Root.Center);
        Assert.Equal(20, doc.Root.Radius, 9);
        Assert.Null(doc.Copyright);
    }

    [Fact]
    public void A_region_is_enclosed_by_its_sphere()
    {
        // A degree square at the equator, up to 1000 m.
        var d = Math.PI / 180;
        var doc = TilesetParser.Parse(Json(
            $"{{\"asset\":{{}},\"root\":{{\"boundingVolume\":{{\"region\":[0,0,{d},{d},0,1000]}},\"geometricError\":1}}}}"),
            Base);
        foreach (var corner in new[] { Wgs84.ToEcef(0, 0, 0), Wgs84.ToEcef(1, 1, 1000), Wgs84.ToEcef(0, 1, 1000) })
        {
            Assert.True((corner - doc.Root.Center).Length <= doc.Root.Radius + 1e-6);
        }

        Assert.InRange(doc.Root.Radius, 70_000, 90_000); // half the diagonal of a 111 km square
    }

    [Fact]
    public void Google_requests_carry_the_key_and_the_session_and_never_leave_the_host()
    {
        var source = new GoogleTileSource(new HttpClient(), "K-TEST");
        Assert.Equal("?key=K-TEST", source.Sign(source.Root).Query);

        // A tileset names the session in the URIs of its children; later URIs without one get it.
        var first = source.Sign(new Uri(Base, "/v1/3dtiles/datasets/x/files/a.json?session=S1"));
        Assert.Contains("session=S1", first.Query);
        Assert.Contains("key=K-TEST", first.Query);
        var later = source.Sign(new Uri(Base, "/v1/3dtiles/datasets/x/files/b.glb"));
        Assert.Contains("session=S1", later.Query);
        Assert.Contains("key=K-TEST", later.Query);

        var elsewhere = Assert.Throws<InvalidOperationException>(() => source.Sign(new Uri("https://example.org/a.glb")));
        Assert.DoesNotContain("K-TEST", elsewhere.Message);
        Assert.Throws<InvalidOperationException>(() => source.Sign(new Uri("http://tile.googleapis.com/a.glb")));
    }
}

public class OsmTests
{
    // An L-shaped footprint of 20 m by 20 m with a 10 m notch, drawn clockwise, 4 levels; and an open way.
    private static string Answer(EnuFrame frame)
    {
        string Node(double east, double north)
        {
            // The geodetic latitude of a point on the ellipsoid: tan(lat) = z / ((1 - e2) p), with WGS84's e2.
            var ecef = frame.ToEcef(new Vec3d(east, north, 0));
            var p = Math.Sqrt(ecef.X * ecef.X + ecef.Y * ecef.Y);
            var (lat, lon) = (Math.Atan2(ecef.Z, (1 - 0.00669437999014) * p) * 180 / Math.PI,
                Math.Atan2(ecef.Y, ecef.X) * 180 / Math.PI);
            return FormattableString.Invariant($"{{\"lat\":{lat:R},\"lon\":{lon:R}}}");
        }

        var l = string.Join(",", Node(0, 0), Node(0, 20), Node(10, 20), Node(10, 10), Node(20, 10), Node(20, 0), Node(0, 0));
        var open = string.Join(",", Node(50, 50), Node(60, 50), Node(60, 60));
        return "{\"elements\":[" +
               "{\"type\":\"way\",\"tags\":{\"building\":\"yes\",\"building:levels\":\"4\"},\"geometry\":[" + l + "]}," +
               "{\"type\":\"way\",\"tags\":{\"building\":\"yes\"},\"geometry\":[" + open + "]}]}";
    }

    [Fact]
    public void A_footprint_becomes_walls_and_a_flat_roof_at_its_height()
    {
        // On the equator geocentric and geodetic latitude agree, so the test's own inverse is exact.
        var frame = new EnuFrame(0, 10, 0);
        var scene = OsmBuildings.Parse(Answer(frame), frame, 0, out var skipped);

        Assert.Equal(1, scene.Tiles);
        Assert.Equal(1, skipped);
        Assert.Equal([OsmBuildings.Attribution], scene.Attributions);
        // 6 walls of 2 triangles, and a roof of 6 - 2 triangles.
        Assert.Equal((6 * 2 + 4) * 3, scene.Vertices);

        var ys = Enumerable.Range(0, scene.Vertices).Select(v => scene.Positions[v * 3 + 1]).ToArray();
        Assert.Equal(0f, ys.Min(), 2);
        Assert.Equal(12f, ys.Max(), 2); // 4 levels of 3 m

        // The roof: the last 4 triangles, facing up, covering the L's 300 m2.
        var area = 0.0;
        for (var t = 12; t < 16; t++)
        {
            float X(int k) => scene.Positions[(t * 3 + k) * 3];
            float Z(int k) => scene.Positions[(t * 3 + k) * 3 + 2];
            Assert.Equal(1f, scene.Normals[t * 9 + 1]);
            area += Math.Abs((X(1) - X(0)) * (Z(2) - Z(0)) - (X(2) - X(0)) * (Z(1) - Z(0))) / 2;
        }

        Assert.Equal(300, area, 0);

        // Every wall faces away from the footprint: a step along its normal leaves the L.
        for (var t = 0; t < 12; t++)
        {
            double mx = 0, mz = 0;
            for (var k = 0; k < 3; k++)
            {
                mx += scene.Positions[(t * 3 + k) * 3] / 3.0;
                mz += scene.Positions[(t * 3 + k) * 3 + 2] / 3.0;
            }

            double e = mx + scene.Normals[t * 9] * 0.5, n = -(mz + scene.Normals[t * 9 + 2] * 0.5);
            var inside = e > 0 && n > 0 && ((e < 10 && n < 20) || (e < 20 && n < 10));
            Assert.False(inside, $"wall {t} faces inward");
        }
    }

    [Fact]
    public void A_stated_height_wins_over_levels_and_a_part_starts_at_its_min_height()
    {
        var frame = new EnuFrame(0, 10, 0);
        var json = Answer(frame).Replace("\"building:levels\":\"4\"", "\"building:levels\":\"4\",\"height\":\"30.5 m\",\"min_height\":\"10\"");
        var scene = OsmBuildings.Parse(json, frame, 0, out _);
        var ys = Enumerable.Range(0, scene.Vertices).Select(v => scene.Positions[v * 3 + 1]).ToArray();
        Assert.Equal(10f, ys.Min(), 2);
        Assert.Equal(30.5f, ys.Max(), 2);
    }
}

public class GlbTests
{
    // A right triangle in the glTF frame: x right, y up.
    private static readonly float[] Positions = [0, 0, 0, 10, 0, 0, 0, 5, 0];
    private static readonly float[] Uvs = [0, 0, 1, 0, 0, 1];
    private static readonly int[] Indices = [0, 1, 2];

    [Fact]
    public void A_plain_tile_comes_out_z_up_and_through_the_node_and_tile_transforms()
    {
        // The node moves the mesh 100 along glTF x; the tile transform moves it 1000 along world z.
        var glb = GlbWriter.Write(Positions, Uvs, Indices, nodeMatrix: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 100, 0, 0, 1],
            copyright: "Data provider A;Data provider B");
        var mesh = GlbReader.Read(glb, new Mat4d([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1000, 1]));

        var p = Assert.Single(mesh.Primitives);
        Assert.Equal(new Vec3d(100, 0, 1000), p.Positions[0]);
        Assert.Equal(new Vec3d(110, 0, 1000), p.Positions[1]);
        Assert.Equal(new Vec3d(100, 0, 1005), p.Positions[2]); // glTF +y is world +z
        Assert.Equal(Uvs, p.Uvs);
        Assert.Equal(Indices, p.Indices);
        Assert.Null(p.Image);
        Assert.Equal("Data provider A;Data provider B", mesh.Copyright);
    }

    [Fact]
    public void A_draco_tile_decodes_to_the_same_triangle()
    {
        var glb = GlbWriter.Write(Positions, Uvs, Indices, draco: true);
        Assert.Contains("KHR_draco_mesh_compression", Encoding.UTF8.GetString(glb));
        var p = Assert.Single(GlbReader.Read(glb, Mat4d.Identity).Primitives);

        Assert.Equal(3, p.Indices.Length);
        // Draco may reorder points: compare the triangle as a set of (position, uv), within the quantization.
        var expected = new[] { (0.0, 0.0, 0.0, 0f, 0f), (10.0, 0.0, 0.0, 1f, 0f), (0.0, 0.0, 5.0, 0f, 1f) };
        foreach (var (x, y, z, u, v) in expected)
        {
            Assert.Contains(p.Indices, i =>
                Math.Abs(p.Positions[i].X - x) < 0.01 && Math.Abs(p.Positions[i].Y - y) < 0.01 &&
                Math.Abs(p.Positions[i].Z - z) < 0.01 && Math.Abs(p.Uvs![i * 2] - u) < 0.01 &&
                Math.Abs(p.Uvs[i * 2 + 1] - v) < 0.01);
        }
    }

    [Fact]
    public void The_base_colour_image_is_returned_as_its_bytes()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        var p = Assert.Single(GlbReader.Read(
            GlbWriter.Write(Positions, Uvs, Indices, image: jpeg, imageMime: "image/jpeg"), Mat4d.Identity).Primitives);
        Assert.Equal(jpeg, p.Image);
        Assert.Equal("image/jpeg", p.ImageMime);
    }

    [Fact]
    public void A_file_that_is_not_a_tile_is_refused_with_a_reason()
    {
        Assert.Contains("magic", Assert.Throws<FormatException>(() =>
            GlbReader.Read(Encoding.UTF8.GetBytes("<html>quota exceeded</html>"), Mat4d.Identity)).Message);

        var cut = GlbWriter.Write(Positions, Uvs, Indices);
        Assert.Throws<FormatException>(() => GlbReader.Read(cut.AsSpan(0, cut.Length - 8), Mat4d.Identity));

        var bad = GlbWriter.Write(Positions, null, [0, 1, 7]);
        Assert.Contains("index 7", Assert.Throws<FormatException>(() => GlbReader.Read(bad, Mat4d.Identity)).Message);
    }
}

public class SceneTests
{
    private static async Task<Scene> Load(double cameraUp, Func<byte[], Pixels?>? decode = null)
    {
        var frame = new EnuFrame(45.4236, -75.7009, 70);
        var options = new SelectionOptions
        {
            Camera = frame.ToEcef(new Vec3d(0, 0, cameraUp)), Target = frame.Origin, Radius = 800,
        };
        return await SceneLoader.LoadAsync(new SyntheticTileSource(45.4236, -75.7009, 70), frame, options,
            decode ?? (_ => null));
    }

    [Fact]
    public async Task The_whole_path_brings_ecef_tiles_to_small_local_coordinates()
    {
        var scene = await Load(cameraUp: 300);

        Assert.Equal(4, scene.Tiles); // the four fine quadrants replace the coarse root
        Assert.Equal(1 + 1 + 4, scene.Requests); // the root, one nested tileset, four meshes
        Assert.False(scene.Truncated);
        Assert.Equal([SyntheticTileSource.Copyright], scene.Attributions);
        Assert.True(scene.Vertices > 4 * 24 * 24 * 6);

        // The tiles were written 6 371 km from the centre of the Earth; here they sit around the origin.
        for (var i = 0; i < scene.Positions.Length; i += 3)
        {
            Assert.InRange(scene.Positions[i], -501f, 501f);
            Assert.InRange(scene.Positions[i + 1], -10f, 200f);
            Assert.InRange(scene.Positions[i + 2], -501f, 501f);
        }

        // The ground under the origin is where the synthetic terrain puts it, to the Draco quantization.
        var nearest = Enumerable.Range(0, scene.Vertices)
            .MinBy(v => MathF.Abs(scene.Positions[v * 3]) + MathF.Abs(scene.Positions[v * 3 + 2]));
        Assert.Equal(SyntheticTileSource.Ground(0, 0), scene.Positions[nearest * 3 + 1], 0);

        // Mostly flat ground seen from above: the normals point up on average.
        var up = Enumerable.Range(0, scene.Vertices).Average(v => scene.Normals[v * 3 + 1]);
        Assert.InRange(up, 0.5, 1.0);
    }

    [Fact]
    public async Task From_far_away_one_coarse_tile_is_fetched()
    {
        var scene = await Load(cameraUp: 50_000);
        Assert.Equal(1, scene.Tiles);
        Assert.Equal(2, scene.Requests);
        Assert.Equal(8 * 8 * 6, scene.Vertices);
    }

    [Fact]
    public async Task Colours_come_from_the_tile_image_when_it_decodes()
    {
        byte[]? seen = null;
        var scene = await Load(300, image =>
        {
            seen = image;
            return new Pixels(1, 1, 3, [255, 0, 0]);
        });
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], seen![..4]); // the tile's PNG, as bytes in memory
        Assert.Equal([1f, 0f, 0f], scene.Colors[..3]);

        // sRGB to linear light: a mid grey of 128 is about a fifth of white, not a half.
        var mid = await Load(300, _ => new Pixels(1, 1, 3, [128, 128, 128]));
        Assert.Equal(0.2195f, mid.Colors[0], 3);

        var grey = await Load(300);
        Assert.Equal(grey.Colors[0], grey.Colors[1]);
    }
}
