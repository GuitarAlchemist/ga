namespace GA.BSP.Core.Spatial;

using System.Collections.Concurrent;
using System.Diagnostics;
using Business.Core.Atonal;
using Microsoft.Extensions.Logging;
using ChordTemplate = Business.Core.Chords.ChordTemplate;

/// <summary>
///     Service for managing and querying the Tonal Binary Space Partitioning tree
///     Optimized with ConcurrentDictionary for thread-safe caching
/// </summary>
public class TonalBspService
{
    private readonly TonalBspTree _bspTree;
    private readonly ILogger<TonalBspService> _logger;

    // ConcurrentDictionary for lock-free thread-safe cache
    private readonly ConcurrentDictionary<string, TonalBspQueryResult> _queryCache;

    public TonalBspService(ILogger<TonalBspService> logger)
    {
        _logger = logger;
        _bspTree = new TonalBspTree();
        _queryCache = new ConcurrentDictionary<string, TonalBspQueryResult>();

        _logger.LogInformation("Tonal BSP Service initialized with tree depth: {Depth}",
            _bspTree.Root.GetDepth());
    }

    /// <summary>
    ///     Find the most appropriate tonal context for a chord
    /// </summary>
    public TonalBspQueryResult FindTonalContextForChord(ChordTemplate chord, PitchClass root)
    {
        var cacheKey = $"chord_{chord.Name}_{root}";

        // GetOrAdd is atomic and thread-safe
        var result = _queryCache.GetOrAdd(cacheKey, _ =>
        {
            var stopwatch = Stopwatch.StartNew();

            var pitchClassSet = chord.PitchClassSet;
            var region = _bspTree.FindTonalRegion(pitchClassSet);
            var elements = _bspTree.QueryRegion(region).ToList();

            var confidence = CalculateContextConfidence(chord, region, elements);
            var depth = CalculateQueryDepth(region);

            stopwatch.Stop();

            _logger.LogDebug("Found tonal context for chord {Chord}: {Region} (confidence: {Confidence:F2})",
                chord.Name, region.Name, confidence);

            return new TonalBspQueryResult(region, elements, confidence, depth, stopwatch.Elapsed);
        });

        return result;
    }

    /// <summary>
    ///     Find tonal context for a pitch class set
    /// </summary>
    public TonalBspQueryResult FindTonalContextForChord(PitchClassSet pitchClassSet)
    {
        var cacheKey = $"pitchset_{pitchClassSet}";

        // GetOrAdd is atomic and thread-safe
        var result = _queryCache.GetOrAdd(cacheKey, _ =>
        {
            var stopwatch = Stopwatch.StartNew();

            var region = _bspTree.FindTonalRegion(pitchClassSet);
            var elements = new List<ITonalElement>
                { new TonalChord("Query", pitchClassSet, region.TonalityType, region.TonalCenter) };
            var confidence = region.Contains(pitchClassSet) ? 0.9 : 0.5;
            var depth = 0;

            stopwatch.Stop();

            return new TonalBspQueryResult(region, elements, confidence, depth, stopwatch.Elapsed);
        });

        return result;
    }

    /// <summary>
    ///     Find related scales for a given pitch class set
    /// </summary>
    public TonalBspQueryResult FindRelatedScales(PitchClassSet pitchClassSet)
    {
        var cacheKey = $"scales_{pitchClassSet}";

        // GetOrAdd is atomic and thread-safe
        var result = _queryCache.GetOrAdd(cacheKey, _ =>
        {
            var stopwatch = Stopwatch.StartNew();

            var region = _bspTree.FindTonalRegion(pitchClassSet);
            var allElements = _bspTree.QueryRegion(region);

            // Filter for scales only
            var scales = allElements.OfType<TonalScale>().Cast<ITonalElement>().ToList();

            var confidence = CalculateScaleCompatibility(pitchClassSet, scales);
            var depth = CalculateQueryDepth(region);

            stopwatch.Stop();

            return new TonalBspQueryResult(region, scales, confidence, depth, stopwatch.Elapsed);
        });

        return result;
    }

    /// <summary>
    ///     Find chord progressions that fit within a tonal region
    /// </summary>
    public TonalBspQueryResult FindProgressionsInRegion(TonalRegion region)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheKey = $"progressions_{region.Name}";

        if (_queryCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var allElements = _bspTree.QueryRegion(region);

        // Filter for chords that could form progressions
        var chords = allElements.OfType<TonalChord>().Cast<ITonalElement>().ToList();

        var confidence = CalculateProgressionCoherence(chords, region);
        var depth = CalculateQueryDepth(region);

        stopwatch.Stop();

        var result = new TonalBspQueryResult(region, chords, confidence, depth, stopwatch.Elapsed);
        _queryCache[cacheKey] = result;

        return result;
    }

    /// <summary>
    ///     Perform spatial query to find elements within a tonal distance
    /// </summary>
    public TonalBspQueryResult SpatialQuery(PitchClassSet center, double radius, TonalPartitionStrategy strategy)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheKey = $"spatial_{center}_{radius}_{strategy}";

        if (_queryCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var results = new List<ITonalElement>();
        var visitedRegions = new HashSet<string>();

        // Perform recursive spatial search
        SpatialQueryRecursive(_bspTree.Root, center, radius, strategy, results, visitedRegions);

        // Create a synthetic region for the query result
        var queryRegion = new TonalRegion(
            $"Spatial Query ({strategy})",
            TonalityType.Atonal,
            center,
            center.Select(pc => (int)pc).Average()
        );

        var confidence = CalculateSpatialConfidence(center, results, radius);
        var depth = 0; // Spatial queries don't have a single depth

        stopwatch.Stop();

        var result = new TonalBspQueryResult(queryRegion, results, confidence, depth, stopwatch.Elapsed);
        _queryCache[cacheKey] = result;

        _logger.LogDebug("Spatial query found {Count} elements within radius {Radius} of {Center}",
            results.Count, radius, center);

        return result;
    }

    /// <summary>
    ///     Get tonal neighbors of a given element
    /// </summary>
    public IEnumerable<ITonalElement> GetTonalNeighbors(ITonalElement element, int maxNeighbors = 10)
    {
        var region = _bspTree.FindTonalRegion(element.PitchClassSet);
        var candidates = _bspTree.QueryRegion(region).Where(e => e != element);

        // Sort by tonal distance and return closest neighbors
        return candidates
            .OrderBy(candidate => CalculateTonalDistance(element, candidate))
            .Take(maxNeighbors);
    }

    /// <summary>
    ///     Analyze tonal relationships between elements
    /// </summary>
    public TonalRelationshipAnalysis AnalyzeTonalRelationships(IEnumerable<ITonalElement> elements)
    {
        var elementList = elements.ToList();
        var relationships = new List<TonalRelationship>();

        for (var i = 0; i < elementList.Count; i++)
        {
            for (var j = i + 1; j < elementList.Count; j++)
            {
                var relationship = AnalyzeRelationship(elementList[i], elementList[j]);
                relationships.Add(relationship);
            }
        }

        return new TonalRelationshipAnalysis
        {
            Elements = elementList,
            Relationships = relationships,
            OverallCoherence = CalculateOverallCoherence(relationships),
            DominantTonality = DetermineDominantTonality(elementList),
            TonalClusters = IdentifyTonalClusters(elementList)
        };
    }

    private void SpatialQueryRecursive(TonalBspNode node, PitchClassSet center, double radius,
        TonalPartitionStrategy strategy, List<ITonalElement> results,
        HashSet<string> visitedRegions)
    {
        if (visitedRegions.Contains(node.Region.Name))
        {
            return;
        }

        visitedRegions.Add(node.Region.Name);

        if (node.IsLeaf)
        {
            // Check each element in the leaf node
            foreach (var element in node.Elements)
            {
                var distance = CalculateTonalDistanceByStrategy(center, element.PitchClassSet, strategy);
                if (distance <= radius)
                {
                    results.Add(element);
                }
            }
        }
        else
        {
            // Check if we need to search child nodes
            var regionDistance = CalculateRegionDistance(center, node.Region, strategy);

            if (regionDistance <= radius)
            {
                // Search both children if region intersects with query radius
                if (node.Left != null)
                {
                    SpatialQueryRecursive(node.Left, center, radius, strategy, results, visitedRegions);
                }

                if (node.Right != null)
                {
                    SpatialQueryRecursive(node.Right, center, radius, strategy, results, visitedRegions);
                }
            }
        }
    }

    private double CalculateContextConfidence(ChordTemplate chord, TonalRegion region, List<ITonalElement> elements)
    {
        // Base confidence on how well the chord fits in the region
        var baseConfidence = region.Contains(chord.PitchClassSet) ? 0.8 : 0.4;

        // Boost confidence if there are related elements in the region
        var relatedElements = elements.Count(e => HasTonalRelationship(chord.PitchClassSet, e.PitchClassSet));
        var relationshipBonus = Math.Min(relatedElements * 0.1, 0.4);

        return Math.Min(baseConfidence + relationshipBonus, 1.0);
    }

    private double CalculateScaleCompatibility(PitchClassSet pitchClassSet, List<ITonalElement> scales)
    {
        if (!scales.Any())
        {
            return 0.0;
        }

        var compatibilityScores = scales.Select(scale =>
            CalculateSetCompatibility(pitchClassSet, scale.PitchClassSet));

        return compatibilityScores.Max();
    }

    private double CalculateSetCompatibility(PitchClassSet set1, PitchClassSet set2)
    {
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0; // Jaccard similarity
    }

    private bool HasTonalRelationship(PitchClassSet set1, PitchClassSet set2)
    {
        // Simple heuristic: sets have a relationship if they share at least 50% of their notes
        var intersection = set1.Intersect(set2).Count();
        var minSize = Math.Min(set1.Cardinality.Value, set2.Cardinality.Value);

        return minSize > 0 && (double)intersection / minSize >= 0.5;
    }

    private double CalculateTonalDistance(ITonalElement element1, ITonalElement element2)
    {
        // Calculate distance based on tonal centers and pitch class set similarity
        var centerDistance = Math.Abs(element1.TonalCenter - element2.TonalCenter);
        centerDistance = Math.Min(centerDistance, 12 - centerDistance); // Circular distance

        var setDistance = 1.0 - CalculateSetCompatibility(element1.PitchClassSet, element2.PitchClassSet);

        return (centerDistance / 6.0 + setDistance) / 2.0; // Normalize and average
    }

    // Additional helper methods...
    private double CalculateProgressionCoherence(List<ITonalElement> chords, TonalRegion region)
    {
        return 0.8;
    }

    private int CalculateQueryDepth(TonalRegion region)
    {
        return 1;
    }

    private double CalculateSpatialConfidence(PitchClassSet center, List<ITonalElement> results, double radius)
    {
        return 0.9;
    }

    private double CalculateTonalDistanceByStrategy(PitchClassSet set1, PitchClassSet set2,
        TonalPartitionStrategy strategy)
    {
        return 0.5;
    }

    private double CalculateRegionDistance(PitchClassSet center, TonalRegion region, TonalPartitionStrategy strategy)
    {
        return 0.3;
    }

    private TonalRelationship AnalyzeRelationship(ITonalElement element1, ITonalElement element2)
    {
        return new TonalRelationship();
    }

    private double CalculateOverallCoherence(List<TonalRelationship> relationships)
    {
        return 0.8;
    }

    private TonalityType DetermineDominantTonality(List<ITonalElement> elements)
    {
        return TonalityType.Major;
    }

    private List<TonalCluster> IdentifyTonalClusters(List<ITonalElement> elements)
    {
        return [];
    }
}

/// <summary>
///     Analysis result for tonal relationships
/// </summary>
public record TonalRelationshipAnalysis
{
    public List<ITonalElement> Elements { get; init; } = [];
    public List<TonalRelationship> Relationships { get; init; } = [];
    public double OverallCoherence { get; init; }
    public TonalityType DominantTonality { get; init; }
    public List<TonalCluster> TonalClusters { get; init; } = [];
}

/// <summary>
///     Represents a relationship between two tonal elements
/// </summary>
public record TonalRelationship
{
    public ITonalElement Element1 { get; init; } = new TonalScale();
    public ITonalElement Element2 { get; init; } = new TonalScale();
    public double Distance { get; init; }
    public string RelationshipType { get; init; } = "";
    public double Strength { get; init; }
}

/// <summary>
///     Represents a cluster of related tonal elements
/// </summary>
public record TonalCluster
{
    public List<ITonalElement> Elements { get; init; } = [];
    public TonalRegion CenterRegion { get; init; } = new();
    public double Cohesion { get; init; }
    public string ClusterType { get; init; } = "";
}
