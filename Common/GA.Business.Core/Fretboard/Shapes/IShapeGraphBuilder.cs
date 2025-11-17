namespace GA.Business.Core.Fretboard.Shapes;

using Atonal;

/// <summary>
/// Interface for building fretboard shape graphs
/// </summary>
public interface IShapeGraphBuilder
{
    /// <summary>
    /// Generate all shapes for a pitch-class set on a given tuning
    /// </summary>
    /// <param name="tuning">Guitar tuning</param>
    /// <param name="pitchClassSet">Pitch-class set to generate shapes for</param>
    /// <param name="options">Build options</param>
    /// <returns>Collection of fretboard shapes</returns>
    IEnumerable<FretboardShape> GenerateShapes(
        Tuning tuning,
        PitchClassSet pitchClassSet,
        ShapeGraphBuildOptions options);

    /// <summary>
    /// Generate shapes asynchronously with streaming support
    /// </summary>
    /// <param name="tuning">Guitar tuning</param>
    /// <param name="pitchClassSet">Pitch-class set to generate shapes for</param>
    /// <param name="options">Build options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of fretboard shapes</returns>
    IAsyncEnumerable<FretboardShape> GenerateShapesStreamAsync(
        Tuning tuning,
        PitchClassSet pitchClassSet,
        ShapeGraphBuildOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Build a complete shape graph for multiple pitch-class sets
    /// </summary>
    /// <param name="tuning">Guitar tuning</param>
    /// <param name="pitchClassSets">Collection of pitch-class sets</param>
    /// <param name="options">Build options</param>
    /// <returns>Complete shape graph with transitions</returns>
    Task<ShapeGraph> BuildGraphAsync(
        Tuning tuning,
        IEnumerable<PitchClassSet> pitchClassSets,
        ShapeGraphBuildOptions options);
}

