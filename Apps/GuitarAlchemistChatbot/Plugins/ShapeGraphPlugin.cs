namespace GuitarAlchemistChatbot.Plugins;

using Services;

/// <summary>
///     Semantic Kernel plugin for shape graph analysis
/// </summary>
public class ShapeGraphPlugin(GaApiClient gaApiClient, ILogger<ShapeGraphPlugin> logger)
{
    /// <summary>
    ///     Analyze a shape graph using comprehensive harmonic analysis
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch classes (0-11, e.g., "0,4,7" for C major triad)</param>
    /// <param name="tuning">Guitar tuning (e.g., "E2 A2 D3 G3 B3 E4" for standard tuning)</param>
    /// <param name="clusterCount">Number of chord families to identify (default: 5)</param>
    /// <returns>Comprehensive analysis including spectral, dynamical, and topological metrics</returns>
    [Description(
        "Analyze a shape graph using spectral graph theory, dynamical systems, and persistent homology to understand chord voicing relationships")]
    public async Task<string> AnalyzeShapeGraphAsync(
        [Description("Comma-separated pitch classes (0-11, e.g., '0,4,7' for C major triad)")]
        string pitchClasses,
        [Description("Guitar tuning (e.g., 'E2 A2 D3 G3 B3 E4' for standard tuning)")]
        string tuning = "E2 A2 D3 G3 B3 E4",
        [Description("Number of chord families to identify (default: 5)")]
        int clusterCount = 5)
    {
        logger.LogInformation(
            "Analyzing shape graph: pitchClasses={PitchClasses}, tuning={Tuning}",
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

        var result = await gaApiClient.AnalyzeShapeGraphAsync(
            pitchClassArray,
            tuning,
            clusterCount);

        if (result == null)
        {
            return "Error: Failed to analyze shape graph. The GaApi service may be unavailable.";
        }

        // Format the response
        var response = $@"**Shape Graph Analysis**

**Chord**: Pitch classes [{string.Join(", ", pitchClassArray)}]
**Tuning**: {tuning}

{FormatSpectralMetrics(result.Spectral)}

{FormatChordFamilies(result.ChordFamilies)}

{FormatCentralShapes(result.CentralShapes)}

{FormatDynamics(result.Dynamics)}

{FormatTopology(result.Topology)}";

        return response;
    }

    private static string FormatSpectralMetrics(SpectralMetricsDto? spectral)
    {
        if (spectral == null)
        {
            return "**Spectral Metrics**: Not available";
        }

        return $@"**Spectral Metrics** (Graph Laplacian Analysis):
- **Algebraic Connectivity**: {spectral.AlgebraicConnectivity:F3} (higher = more connected)
- **Spectral Gap**: {spectral.SpectralGap:F3} (higher = better clustering)
- **Connected Components**: {spectral.ComponentCount}";
    }

    private static string FormatChordFamilies(List<ChordFamilyDto> families)
    {
        if (families == null || families.Count == 0)
        {
            return "**Chord Families**: None identified";
        }

        var formatted = families.Take(5).Select(f =>
            $"- **Family {f.ClusterId}**: {f.ShapeIds.Count} shapes (representative: {f.Representative})");

        return $@"**Chord Families** (Spectral Clustering):
{string.Join("\n", formatted)}";
    }

    private static string FormatCentralShapes(List<CentralShapeDto> centralShapes)
    {
        if (centralShapes == null || centralShapes.Count == 0)
        {
            return "**Central Shapes**: None identified";
        }

        var formatted = centralShapes.Take(5).Select((s, i) =>
            $"{i + 1}. **{s.ShapeId}** (centrality: {s.Centrality:F3})");

        return $@"**Most Central Shapes** (PageRank):
{string.Join("\n", formatted)}

These shapes are the most important in the voicing network - learn these first!";
    }

    private static string FormatDynamics(DynamicsDto? dynamics)
    {
        if (dynamics == null)
        {
            return "**Dynamical System Analysis**: Not available";
        }

        var attractorInfo = dynamics.Attractors.Count > 0
            ? string.Join("\n", dynamics.Attractors.Take(3).Select(a =>
                $"  - **{a.ShapeId}** (basin size: {a.BasinSize})"))
            : "  None identified";

        var limitCycleInfo = dynamics.LimitCycles.Count > 0
            ? string.Join("\n", dynamics.LimitCycles.Take(3).Select(lc =>
                $"  - Period {lc.Period}: {string.Join(" → ", lc.ShapeIds.Take(3))}... (strength: {lc.Stability:F2})"))
            : "  None identified";

        var chaosInfo = dynamics.IsChaotic
            ? "**Chaotic system**: Unpredictable long-term behavior"
            : dynamics.IsStable
                ? "**Stable system**: Predictable, converges to attractors"
                : "**Neutral system**: Neither chaotic nor stable";

        return $@"**Dynamical System Analysis**:
- **Lyapunov Exponent**: {dynamics.LyapunovExponent:F3}
- {chaosInfo}

**Attractors** (stable voicings that progressions tend toward):
{attractorInfo}

**Limit Cycles** (common progression patterns):
{limitCycleInfo}";
    }

    private static string FormatTopology(TopologyDto? topology)
    {
        if (topology == null)
        {
            return "**Topological Analysis**: Not available";
        }

        var featureInfo = topology.Features.Count > 0
            ? string.Join("\n", topology.Features.Take(3).Select(f =>
                $"  - H{f.Dimension}: birth={f.Birth:F2}, death={f.Death:F2}, persistence={f.Persistence:F2}"))
            : "  None identified";

        return $@"**Topological Analysis** (Persistent Homology):
- **Betti Number β₀**: {topology.BettiNumber0} (connected components)
- **Betti Number β₁**: {topology.BettiNumber1} (loops/cycles)

**Persistent Features**:
{featureInfo}";
    }
}
