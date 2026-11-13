namespace GeoTiles;

/// <summary>What the selection is asked: where the eye is, what part of the world matters, and how much error in
/// pixels is tolerated.</summary>
public sealed record SelectionOptions
{
    public required Vec3d Camera { get; init; }

    /// <summary>Only tiles that touch the sphere (<see cref="Target"/>, <see cref="Radius"/>) are kept: the demo
    /// shows one place, not the whole view frustum.</summary>
    public required Vec3d Target { get; init; }

    public required double Radius { get; init; }
    public double FovYRadians { get; init; } = Math.PI / 3;
    public int ScreenHeight { get; init; } = 720;

    /// <summary>The tolerated screen-space error in pixels; 16 is the usual default of 3D Tiles viewers.</summary>
    public double MaxScreenSpaceError { get; init; } = 16;

    /// <summary>A cap on the meshes to fetch, so a mistake costs a bounded number of requests.</summary>
    public int MaxTiles { get; init; } = 200;
}

public sealed record SelectedTile(Uri Content, Mat4d Transform, double GeometricError, int Depth);

public sealed record SelectionResult(IReadOnlyList<SelectedTile> Tiles, bool Truncated, int TilesetsLoaded,
    IReadOnlyList<string> Copyrights);

public static class TileSelector
{
    /// <summary>The error of a tile on screen, in pixels: its geometric error in metres, seen from
    /// <paramref name="distance"/> metres, on a screen <paramref name="screenHeight"/> pixels high.</summary>
    public static double ScreenSpaceError(double geometricError, double distance, int screenHeight, double fovY) =>
        geometricError * screenHeight / (2 * Math.Max(distance, 1e-3) * Math.Tan(fovY / 2));

    /// <summary>Walks the tree from <paramref name="root"/>, loading the external tilesets it meets through
    /// <paramref name="loadTileset"/>, and returns the meshes to draw.</summary>
    public static async Task<SelectionResult> SelectAsync(Tile root, SelectionOptions options,
        Func<Uri, Tile, Task<TilesetDocument>> loadTileset)
    {
        var selected = new List<SelectedTile>();
        var copyrights = new List<string>();
        var truncated = false;
        var tilesets = 0;

        async Task Visit(Tile tile, int depth)
        {
            if (truncated || (tile.Center - options.Target).Length > tile.Radius + options.Radius)
            {
                return;
            }

            var distance = (tile.Center - options.Camera).Length - tile.Radius;
            var sse = ScreenSpaceError(tile.GeometricError, distance, options.ScreenHeight, options.FovYRadians);
            var meshes = new List<Uri>();
            var children = new List<Tile>(tile.Children);
            foreach (var content in tile.Contents)
            {
                if (!Tile.IsTileset(content))
                {
                    meshes.Add(content);
                }
                else if (sse > options.MaxScreenSpaceError || tile.Children.Count == 0 && meshes.Count == 0)
                {
                    // An external tileset is this tile's children, written elsewhere.
                    var doc = await loadTileset(content, tile);
                    tilesets++;
                    if (!string.IsNullOrWhiteSpace(doc.Copyright) && !copyrights.Contains(doc.Copyright))
                    {
                        copyrights.Add(doc.Copyright);
                    }

                    children.Add(doc.Root);
                }
            }

            var refine = sse > options.MaxScreenSpaceError && children.Count > 0;
            if (!refine || !tile.Replace)
            {
                foreach (var mesh in meshes)
                {
                    if (selected.Count >= options.MaxTiles)
                    {
                        truncated = true;
                        return;
                    }

                    selected.Add(new SelectedTile(mesh, tile.Transform, tile.GeometricError, depth));
                }
            }

            if (refine || meshes.Count == 0)
            {
                foreach (var child in children)
                {
                    await Visit(child, depth + 1);
                }
            }
        }

        await Visit(root, 0);
        return new SelectionResult(selected, truncated, tilesets, copyrights);
    }
}
