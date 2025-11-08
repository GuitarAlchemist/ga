namespace GuitarAlchemistChatbot.Plugins;

using Services;

/// <summary>
///     Semantic Kernel plugin for BSP dungeon generation
/// </summary>
public class BSPDungeonPlugin(GaApiClient gaApiClient, ILogger<BSPDungeonPlugin> logger)
{
    /// <summary>
    ///     Generate a musical dungeon using Binary Space Partitioning
    /// </summary>
    /// <param name="width">Width of the dungeon (default: 80)</param>
    /// <param name="height">Height of the dungeon (default: 60)</param>
    /// <param name="maxDepth">Maximum BSP tree depth (default: 4)</param>
    /// <param name="seed">Optional seed for reproducible generation</param>
    /// <returns>Dungeon layout with rooms and corridors</returns>
    [Description("Generate a musical dungeon using Binary Space Partitioning algorithm")]
    public async Task<string> GenerateDungeonAsync(
        [Description("Width of the dungeon (default: 80)")]
        int width = 80,
        [Description("Height of the dungeon (default: 60)")]
        int height = 60,
        [Description("Maximum BSP tree depth (default: 4)")]
        int maxDepth = 4,
        [Description("Optional seed for reproducible generation")]
        int? seed = null)
    {
        logger.LogInformation("Generating BSP dungeon: {Width}x{Height}, depth={Depth}", width, height, maxDepth);

        var result = await gaApiClient.GenerateDungeonAsync(width, height, maxDepth, seed);

        if (result == null)
        {
            return "Error: Failed to generate dungeon. The GaApi service may be unavailable.";
        }

        var response = $@"**BSP Dungeon Generated** 🏰

**Dimensions**: {result.Width} x {result.Height}
**Seed**: {result.Seed ?? 0}
**Rooms**: {result.Rooms.Count}
**Corridors**: {result.Corridors.Count}

**Room Details**:
{FormatRooms(result.Rooms)}

**Statistics**:
- Average room size: {result.Rooms.Average(r => r.Width * r.Height):F1} tiles
- Total dungeon area: {result.Width * result.Height} tiles
- Room coverage: {result.Rooms.Sum(r => r.Width * r.Height) / (double)(result.Width * result.Height) * 100:F1}%

The dungeon has been generated using Binary Space Partitioning!";

        return response;
    }

    /// <summary>
    ///     Generate an intelligent musical dungeon with analyzer-powered features
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch classes (0-11, e.g., "0,4,7" for C major triad)</param>
    /// <param name="tuning">Guitar tuning (e.g., "E2 A2 D3 G3 B3 E4" for standard tuning)</param>
    /// <param name="width">Width of the dungeon (default: 100)</param>
    /// <param name="height">Height of the dungeon (default: 100)</param>
    /// <returns>Intelligent dungeon with musical landmarks and challenge paths</returns>
    [Description(
        "Generate an intelligent musical dungeon with spectral analysis, attractors, and optimal learning paths")]
    public async Task<string> GenerateIntelligentDungeonAsync(
        [Description("Comma-separated pitch classes (0-11, e.g., '0,4,7' for C major triad)")]
        string pitchClasses,
        [Description("Guitar tuning (e.g., 'E2 A2 D3 G3 B3 E4' for standard tuning)")]
        string tuning = "E2 A2 D3 G3 B3 E4",
        [Description("Width of the dungeon (default: 100)")]
        int width = 100,
        [Description("Height of the dungeon (default: 100)")]
        int height = 100)
    {
        logger.LogInformation(
            "Generating intelligent BSP dungeon: pitchClasses={PitchClasses}, tuning={Tuning}",
            pitchClasses,
            tuning);

        // Parse pitch classes
        var pitchClassArray = pitchClasses
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(pc => int.TryParse(pc.Trim(), out var value) ? value : -1)
            .Where(pc => pc >= 0 && pc <= 11)
            .ToArray();

        if (pitchClassArray.Length == 0)
        {
            return
                "Error: No valid pitch classes provided. Please provide pitch classes as comma-separated numbers (0-11).";
        }

        var result = await gaApiClient.GenerateIntelligentDungeonAsync(
            pitchClassArray,
            tuning,
            width,
            height);

        if (result == null)
        {
            return "Error: Failed to generate intelligent dungeon. The GaApi service may be unavailable.";
        }

        var response = $@"**Intelligent Musical Dungeon Generated** 🎸🏰

**Chord**: Pitch classes [{string.Join(", ", pitchClassArray)}]
**Tuning**: {tuning}
**Dimensions**: {result.Width} x {result.Height}

**Floors**: {result.Floors.Count} (based on chord families)
{FormatFloors(result.Floors)}

**Landmarks**: {result.Landmarks.Count} (central shapes with high PageRank)
{FormatLandmarks(result.Landmarks)}

**Portals**: {result.Portals.Count} (bridge chords connecting families)
{FormatPortals(result.Portals)}

**Safe Zones**: {result.SafeZones.Count} (attractor basins)
{FormatSafeZones(result.SafeZones)}

**Challenge Paths**: {result.ChallengePaths.Count} (limit cycles)
{FormatChallengePaths(result.ChallengePaths)}

**Optimal Learning Path**: {result.LearningPath.ShapeIds.Count} steps
Quality: {result.LearningPath.Quality:F2}, Diversity: {result.LearningPath.Diversity:F2}

This dungeon uses ALL 9 advanced mathematical techniques for musical intelligence!";

        return response;
    }

    private static string FormatRooms(List<DungeonRoom> rooms)
    {
        if (rooms.Count == 0)
        {
            return "No rooms generated.";
        }

        var formatted = rooms.Take(5).Select((r, i) =>
            $"{i + 1}. Room at ({r.X}, {r.Y}), size {r.Width}x{r.Height}, center ({r.CenterX}, {r.CenterY})");

        var more = rooms.Count > 5 ? $"\n... and {rooms.Count - 5} more rooms" : "";

        return string.Join("\n", formatted) + more;
    }

    private static string FormatFloors(List<DungeonFloor> floors)
    {
        if (floors.Count == 0)
        {
            return "No floors generated.";
        }

        var formatted = floors.Take(3).Select((f, i) =>
            $"{i + 1}. Floor {f.FloorNumber}: {f.ShapeCount} shapes, family {f.FamilyId}");

        return string.Join("\n", formatted);
    }

    private static string FormatLandmarks(List<DungeonLandmark> landmarks)
    {
        if (landmarks.Count == 0)
        {
            return "No landmarks placed.";
        }

        var formatted = landmarks.Take(3).Select((l, i) =>
            $"{i + 1}. {l.Name} at ({l.X}, {l.Y}) - centrality: {l.Centrality:F2}");

        return string.Join("\n", formatted);
    }

    private static string FormatPortals(List<DungeonPortal> portals)
    {
        if (portals.Count == 0)
        {
            return "No portals created.";
        }

        var formatted = portals.Take(3).Select((p, i) =>
            $"{i + 1}. Portal from floor {p.FromFloor} to {p.ToFloor} - betweenness: {p.Betweenness:F2}");

        return string.Join("\n", formatted);
    }

    private static string FormatSafeZones(List<DungeonSafeZone> safeZones)
    {
        if (safeZones.Count == 0)
        {
            return "No safe zones identified.";
        }

        var formatted = safeZones.Take(3).Select((z, i) =>
            $"{i + 1}. Safe zone at ({z.X}, {z.Y}) - basin size: {z.BasinSize:F2}");

        return string.Join("\n", formatted);
    }

    private static string FormatChallengePaths(List<DungeonChallengePath> paths)
    {
        if (paths.Count == 0)
        {
            return "No challenge paths created.";
        }

        var formatted = paths.Take(3).Select((p, i) =>
            $"{i + 1}. Challenge path with {p.ShapeIds.Count} steps - period: {p.Period}");

        return string.Join("\n", formatted);
    }
}
