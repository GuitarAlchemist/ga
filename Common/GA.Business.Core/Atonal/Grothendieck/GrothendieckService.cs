namespace GA.Business.Core.Atonal.Grothendieck;

using System.Buffers;

/// <summary>
///     Service for Grothendieck group operations on pitch-class sets
/// </summary>
[PublicAPI]
public class GrothendieckService(ILogger<GrothendieckService> logger) : IGrothendieckService
{
    /// <inheritdoc />
    public IntervalClassVector ComputeICV(IEnumerable<int> pitchClasses)
    {
        var pcs = pitchClasses.Select(pc => PitchClass.FromValue(pc % 12)).ToList();
        var pitchClassSet = new PitchClassSet(pcs);
        return pitchClassSet.IntervalClassVector;
    }

    /// <inheritdoc />
    public GrothendieckDelta ComputeDelta(IntervalClassVector source, IntervalClassVector target)
    {
        return GrothendieckDelta.FromICVs(source, target);
    }

    /// <inheritdoc />
    public double ComputeHarmonicCost(GrothendieckDelta delta)
    {
        // Use L1 norm as harmonic cost
        // Weight can be adjusted based on musical context
        return delta.L1Norm * 0.6; // 0.6 is a scaling factor
    }

    /// <inheritdoc />
    public IEnumerable<(PitchClassSet Set, GrothendieckDelta Delta, double Cost)> FindNearby(
        PitchClassSet source,
        int maxDistance)
    {
        logger.LogInformation(
            "Finding pitch-class sets within distance {MaxDistance} of {Source}",
            maxDistance,
            source
        );

        var sourceIcv = source.IntervalClassVector;
        var results = new List<(PitchClassSet, GrothendieckDelta, double)>();

        // Check all pitch-class sets
        foreach (var candidate in PitchClassSet.Items)
        {
            var delta = ComputeDelta(sourceIcv, candidate.IntervalClassVector);

            if (delta.L1Norm <= maxDistance)
            {
                var cost = ComputeHarmonicCost(delta);
                results.Add((candidate, delta, cost));
            }
        }

        // Sort by cost (ascending)
        return results.OrderBy(r => r.Item3);
    }

    /// <inheritdoc />
    public IEnumerable<PitchClassSet> FindShortestPath(
        PitchClassSet source,
        PitchClassSet target,
        int maxSteps = 5)
    {
        logger.LogInformation(
            "Finding shortest path from {Source} to {Target} (max {MaxSteps} steps)",
            source,
            target,
            maxSteps
        );

        // Simple breadth-first search
        var queue = new Queue<(PitchClassSet Current, List<PitchClassSet> Path)>();
        var visited = new HashSet<PitchClassSet> { source };

        queue.Enqueue((source, [source]));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            // Check if we've reached the target
            if (current == target)
            {
                return path;
            }

            // Check if we've exceeded max steps
            if (path.Count >= maxSteps + 1)
            {
                continue;
            }

            // Find nearby sets (within distance 2)
            var nearby = FindNearby(current, 2)
                .Select(r => r.Set)
                .Where(s => !visited.Contains(s));

            foreach (var next in nearby)
            {
                visited.Add(next);
                var newPath = new List<PitchClassSet>(path) { next };
                queue.Enqueue((next, newPath));
            }
        }

        // No path found
        logger.LogWarning("No path found from {Source} to {Target}", source, target);
        return [];
    }

    /// <summary>
    ///     Compute ICV from ReadOnlySpan for zero-allocation performance
    /// </summary>
    /// <param name="pitchClasses">Pitch classes (0-11) as span</param>
    /// <returns>Interval-class vector</returns>
    public IntervalClassVector ComputeICV(ReadOnlySpan<int> pitchClasses)
    {
        // Use ArrayPool for temporary storage to avoid allocation
        var pool = ArrayPool<PitchClass>.Shared;
        var pcArray = pool.Rent(pitchClasses.Length);

        try
        {
            // Convert to PitchClass instances
            for (var i = 0; i < pitchClasses.Length; i++)
            {
                pcArray[i] = PitchClass.FromValue(pitchClasses[i] % 12);
            }

            // Create PitchClassSet from span
            var pcs = new List<PitchClass>(pitchClasses.Length);
            for (var i = 0; i < pitchClasses.Length; i++)
            {
                pcs.Add(pcArray[i]);
            }

            var pitchClassSet = new PitchClassSet(pcs);
            return pitchClassSet.IntervalClassVector;
        }
        finally
        {
            // Return array to pool
            pool.Return(pcArray, true);
        }
    }
}
