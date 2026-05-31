namespace GA.Domain.Services.Atonal.Grothendieck;

using GA.Domain.Core.Theory.Atonal;

using System.Buffers;
using System.Collections.Concurrent;

/// <summary>
///     Service for Grothendieck group operations on pitch-class sets
/// </summary>
[PublicAPI]
public class GrothendieckService : IGrothendieckService
{
    // FindNearby is pure: (source, maxDistance) → list, deterministic and
    // PitchClassSet.Items is static. The chatbot hits this from the hot
    // path (IcvNeighborsSkill, ChordSubstitutionSkill, ICV-badge augmentation,
    // FindShortestPath BFS) — typically 2^12 PitchClassSet enumerations
    // per call. Cache the result keyed by source+distance. Realistic working
    // set is ~50 distinct keys; cap at NearbyCacheCapacity to bound memory
    // when an adversarial caller probes the whole space.
    private const int NearbyCacheCapacity = 256;
    private const int NearbyCacheEvictionBatch = 32;
    private readonly ConcurrentDictionary<(PitchClassSet, int), IReadOnlyList<(PitchClassSet Set, GrothendieckDelta Delta, double Cost)>> _findNearbyCache = new();
    private readonly Lock _findNearbyEvictionLock = new();
    private long _findNearbyInsertSeq;
    private readonly ConcurrentDictionary<(PitchClassSet, int), long> _findNearbyInsertOrder = new();
    /// <inheritdoc />
    public IntervalClassVector ComputeIcv(IEnumerable<int> pitchClasses)
    {
        var pcs = pitchClasses.Select(pc => PitchClass.FromValue(pc % 12)).ToList();
        var pitchClassSet = new PitchClassSet(pcs);
        return pitchClassSet.IntervalClassVector;
    }

    /// <inheritdoc />
    public GrothendieckDelta ComputeDelta(IntervalClassVector source, IntervalClassVector target) => GrothendieckDelta.FromIcVs(source, target);

    /// <inheritdoc />
    public double ComputeHarmonicCost(GrothendieckDelta delta) =>
        // Use L1 norm as harmonic cost
        // Weight can be adjusted based on musical context
        delta.L1Norm * 0.6; // 0.6 is a scaling factor

    /// <inheritdoc />
    public IEnumerable<(PitchClassSet Set, GrothendieckDelta Delta, double Cost)> FindNearby(
        PitchClassSet source,
        int maxDistance)
    {
        var key = (source, maxDistance);
        if (_findNearbyCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var sourceIcv = source.IntervalClassVector;
        var results = new List<(PitchClassSet, GrothendieckDelta, double)>();

        // Always include the source set itself at distance 0 for identity semantics
        if (maxDistance >= 0)
        {
            results.Add((source, GrothendieckDelta.Zero, 0.0));
        }

        // Check all pitch-class sets
        foreach (var candidate in PitchClassSet.Items)
        {
            var delta = ComputeDelta(sourceIcv, candidate.IntervalClassVector);

            if (delta.L1Norm <= maxDistance)
            {
                var cost = ComputeHarmonicCost(delta);
                // Avoid adding duplicate self-entry (cost already added above)
                if (!ReferenceEquals(candidate, source))
                {
                    results.Add((candidate, delta, cost));
                }
            }
        }

        // Sort by cost (ascending), materialize so the cached value is stable
        var sorted = results.OrderBy(r => r.Item3).ToList().AsReadOnly();

        if (_findNearbyCache.TryAdd(key, sorted))
        {
            _findNearbyInsertOrder[key] = Interlocked.Increment(ref _findNearbyInsertSeq);
            EvictFindNearbyIfOverCapacity();
        }

        return sorted;
    }

    private void EvictFindNearbyIfOverCapacity()
    {
        if (_findNearbyCache.Count <= NearbyCacheCapacity) return;

        // Serialize eviction so two concurrent overflows don't double-prune.
        // The capacity check above is racy by design — entering the lock revalidates.
        lock (_findNearbyEvictionLock)
        {
            if (_findNearbyCache.Count <= NearbyCacheCapacity) return;

            var victims = _findNearbyInsertOrder
                .OrderBy(kvp => kvp.Value)
                .Take(NearbyCacheEvictionBatch)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in victims)
            {
                _findNearbyCache.TryRemove(key, out _);
                _findNearbyInsertOrder.TryRemove(key, out _);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<PitchClassSet> FindShortestPath(
        PitchClassSet source,
        PitchClassSet target,
        int maxSteps = 5)
    {
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

            // Find nearby sets (within small distance) to keep the graph sparse and paths musically local.
            // Using radius=2 connects closely related diatonic collections (e.g., C major → G major)
            // that typically differ by one accidental yet may exceed radius=1 under the ICV L1 metric.
            var nearby = FindNearby(current, 2)
                .Select(r => r.Set)
                // Restrict traversal to sets with the same cardinality to avoid unrealistic one-step jumps
                .Where(s => s.Cardinality == current.Cardinality)
                .Where(s => !visited.Contains(s));

            foreach (var next in nearby)
            {
                visited.Add(next);
                var newPath = new List<PitchClassSet>(path) { next };
                queue.Enqueue((next, newPath));
            }
        }

        // No path found
        return [];
    }

    /// <summary>
    ///     Compute ICV from ReadOnlySpan for zero-allocation performance
    /// </summary>
    /// <param name="pitchClasses">Pitch classes (0-11) as span</param>
    /// <returns>Interval-class vector</returns>
    public IntervalClassVector ComputeIcv(ReadOnlySpan<int> pitchClasses)
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
