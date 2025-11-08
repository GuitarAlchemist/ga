namespace GA.BSP.Core.Spatial;

using System.Numerics;
using Business.Core.Atonal;

/// <summary>
///     Binary Space Partitioning for Tonal Space
///     Hierarchically partitions musical space using tonal relationships
/// </summary>
public class TonalBspTree
{
    public TonalBspTree()
    {
        Root = BuildTonalHierarchy();
    }

    public TonalBspNode Root { get; }

    /// <summary>
    ///     Find the most appropriate tonal region for a given musical element
    /// </summary>
    public TonalRegion FindTonalRegion(PitchClassSet pitchClassSet)
    {
        return FindRegionRecursive(Root, pitchClassSet);
    }

    /// <summary>
    ///     Get all musical elements within a specific tonal region
    /// </summary>
    public IEnumerable<ITonalElement> QueryRegion(TonalRegion region)
    {
        return QueryRegionRecursive(Root, region);
    }

    private TonalRegion FindRegionRecursive(TonalBspNode node, PitchClassSet pitchClassSet)
    {
        if (node.IsLeaf)
        {
            return node.Region;
        }

        // Use tonal distance to determine which side of the partition
        var tonalDistance = CalculateTonalDistance(pitchClassSet, node.PartitionPlane);

        if (tonalDistance <= 0)
        {
            return FindRegionRecursive(node.Left!, pitchClassSet);
        }

        return FindRegionRecursive(node.Right!, pitchClassSet);
    }

    private IEnumerable<ITonalElement> QueryRegionRecursive(TonalBspNode node, TonalRegion targetRegion)
    {
        if (node.IsLeaf)
        {
            if (node.Region.Equals(targetRegion))
            {
                return node.Elements;
            }

            return [];
        }

        var results = new List<ITonalElement>();

        // Check if target region intersects with left subtree
        if (RegionIntersects(targetRegion, node.Left!.Region))
        {
            results.AddRange(QueryRegionRecursive(node.Left, targetRegion));
        }

        // Check if target region intersects with right subtree  
        if (RegionIntersects(targetRegion, node.Right!.Region))
        {
            results.AddRange(QueryRegionRecursive(node.Right, targetRegion));
        }

        return results;
    }

    private TonalBspNode BuildTonalHierarchy()
    {
        // Start with the complete chromatic space
        var chromaticSpace = new TonalRegion(
            "Chromatic",
            TonalityType.Atonal,
            new PitchClassSet([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
            0.0 // No tonal center
        );

        return PartitionTonalSpace(chromaticSpace, GetAllTonalElements(), 0);
    }

    private TonalBspNode PartitionTonalSpace(TonalRegion region, List<ITonalElement> elements, int depth)
    {
        // Base case: create leaf node if we have few elements or reached max depth
        if (elements.Count <= 10 || depth >= 8)
        {
            return new TonalBspNode
            {
                Region = region,
                Elements = elements,
                IsLeaf = true
            };
        }

        // Find the best partition plane for this tonal region
        var partitionPlane = FindBestTonalPartition(region, elements);

        // Split elements based on the partition
        var (leftElements, rightElements) = SplitElementsByTonalPartition(elements, partitionPlane);

        // Create child regions
        var leftRegion = CreateChildRegion(region, partitionPlane, true);
        var rightRegion = CreateChildRegion(region, partitionPlane, false);

        // Recursively build subtrees
        var leftChild = PartitionTonalSpace(leftRegion, leftElements, depth + 1);
        var rightChild = PartitionTonalSpace(rightRegion, rightElements, depth + 1);

        return new TonalBspNode
        {
            Region = region,
            PartitionPlane = partitionPlane,
            Left = leftChild,
            Right = rightChild,
            IsLeaf = false
        };
    }

    private TonalPartitionPlane FindBestTonalPartition(TonalRegion region, List<ITonalElement> elements)
    {
        var bestPartition = new TonalPartitionPlane();
        var bestScore = double.MinValue;

        // Try different partition strategies
        var strategies = new[]
        {
            TonalPartitionStrategy.CircleOfFifths,
            TonalPartitionStrategy.ChromaticDistance,
            TonalPartitionStrategy.HarmonicSeries,
            TonalPartitionStrategy.ModalBrightness,
            TonalPartitionStrategy.TonalStability
        };

        foreach (var strategy in strategies)
        {
            var partition = CreatePartitionPlane(region, strategy);
            var score = EvaluatePartition(elements, partition);

            if (score > bestScore)
            {
                bestScore = score;
                bestPartition = partition;
            }
        }

        return bestPartition;
    }

    private TonalPartitionPlane CreatePartitionPlane(TonalRegion region, TonalPartitionStrategy strategy)
    {
        return strategy switch
        {
            TonalPartitionStrategy.CircleOfFifths => new TonalPartitionPlane
            {
                Strategy = strategy,
                ReferencePoint = region.TonalCenter,
                Normal = CalculateCircleOfFifthsNormal(region.TonalCenter),
                Threshold = 0.5
            },
            TonalPartitionStrategy.ChromaticDistance => new TonalPartitionPlane
            {
                Strategy = strategy,
                ReferencePoint = region.TonalCenter,
                Normal = Vector3.UnitX, // Chromatic axis
                Threshold = 6.0 // Tritone
            },
            TonalPartitionStrategy.HarmonicSeries => new TonalPartitionPlane
            {
                Strategy = strategy,
                ReferencePoint = region.TonalCenter,
                Normal = CalculateHarmonicSeriesNormal(region.TonalCenter),
                Threshold = 0.618 // Golden ratio
            },
            TonalPartitionStrategy.ModalBrightness => new TonalPartitionPlane
            {
                Strategy = strategy,
                ReferencePoint = region.TonalCenter,
                Normal = Vector3.UnitY, // Brightness axis
                Threshold = 0.0 // Neutral brightness
            },
            TonalPartitionStrategy.TonalStability => new TonalPartitionPlane
            {
                Strategy = strategy,
                ReferencePoint = region.TonalCenter,
                Normal = Vector3.UnitZ, // Stability axis
                Threshold = 0.7 // High stability threshold
            },
            _ => throw new ArgumentException($"Unknown partition strategy: {strategy}")
        };
    }

    private Vector3 CalculateCircleOfFifthsNormal(double tonalCenter)
    {
        // Map tonal center to circle of fifths position
        var fifthsPosition = tonalCenter * 7 % 12; // Multiply by 7 for fifths
        var angle = fifthsPosition / 12.0 * 2 * Math.PI;

        return new Vector3(
            (float)Math.Cos(angle),
            (float)Math.Sin(angle),
            0
        );
    }

    private Vector3 CalculateHarmonicSeriesNormal(double tonalCenter)
    {
        // Use harmonic series ratios to determine normal
        var harmonicRatio = Math.Log2((tonalCenter + 1) / 1.0); // Log of harmonic ratio

        return new Vector3(
            (float)Math.Cos(harmonicRatio),
            (float)Math.Sin(harmonicRatio),
            (float)(harmonicRatio / Math.PI)
        );
    }

    private double EvaluatePartition(List<ITonalElement> elements, TonalPartitionPlane partition)
    {
        var (leftElements, rightElements) = SplitElementsByTonalPartition(elements, partition);

        // Evaluate balance (prefer roughly equal splits)
        var balance = 1.0 - Math.Abs(leftElements.Count - rightElements.Count) / (double)elements.Count;

        // Evaluate tonal coherence within each partition
        var leftCoherence = CalculateTonalCoherence(leftElements);
        var rightCoherence = CalculateTonalCoherence(rightElements);

        // Evaluate separation between partitions
        var separation = CalculateTonalSeparation(leftElements, rightElements);

        // Weighted combination of factors
        return 0.3 * balance + 0.4 * (leftCoherence + rightCoherence) / 2.0 + 0.3 * separation;
    }

    private double CalculateTonalDistance(PitchClassSet pitchClassSet, TonalPartitionPlane plane)
    {
        // Convert pitch class set to tonal coordinates
        var tonalCoords = ConvertToTonalCoordinates(pitchClassSet);

        return plane.Strategy switch
        {
            TonalPartitionStrategy.CircleOfFifths => CalculateCircleOfFifthsDistance(tonalCoords, plane),
            TonalPartitionStrategy.ChromaticDistance => CalculateChromaticDistance(tonalCoords, plane),
            TonalPartitionStrategy.HarmonicSeries => CalculateHarmonicDistance(tonalCoords, plane),
            TonalPartitionStrategy.ModalBrightness => CalculateModalBrightnessDistance(tonalCoords, plane),
            TonalPartitionStrategy.TonalStability => CalculateTonalStabilityDistance(tonalCoords, plane),
            _ => 0.0
        };
    }

    private Vector3 ConvertToTonalCoordinates(PitchClassSet pitchClassSet)
    {
        // Map pitch class set to 3D tonal space
        var centroid = pitchClassSet.Select(pc => (int)pc).Average();
        var spread = CalculateSpread(pitchClassSet);
        var brightness = CalculateBrightness(pitchClassSet);

        return new Vector3((float)centroid, (float)brightness, (float)spread);
    }

    private double CalculateSpread(PitchClassSet pitchClassSet)
    {
        if (pitchClassSet.Cardinality.Value <= 1)
        {
            return 0.0;
        }

        var pitchClasses = pitchClassSet.Select(pc => (int)pc).ToArray();
        var variance = pitchClasses.Select(pc => Math.Pow(pc - pitchClasses.Average(), 2)).Average();
        return Math.Sqrt(variance) / 6.0; // Normalize to [0,1]
    }

    private double CalculateBrightness(PitchClassSet pitchClassSet)
    {
        // Higher pitch classes are "brighter"
        return pitchClassSet.Select(pc => (int)pc).Average() / 11.0; // Normalize to [0,1]
    }

    private List<ITonalElement> GetAllTonalElements()
    {
        // This would be populated with actual musical elements from your system
        var elements = new List<ITonalElement>();

        // Add major scales
        for (var root = 0; root < 12; root++)
        {
            elements.Add(new TonalScale(
                $"{GetNoteName(root)} Major",
                new PitchClassSet([
                    root, (root + 2) % 12, (root + 4) % 12, (root + 5) % 12,
                    (root + 7) % 12, (root + 9) % 12, (root + 11) % 12
                ]),
                TonalityType.Major,
                root
            ));
        }

        // Add minor scales
        for (var root = 0; root < 12; root++)
        {
            elements.Add(new TonalScale(
                $"{GetNoteName(root)} Minor",
                new PitchClassSet([
                    root, (root + 2) % 12, (root + 3) % 12, (root + 5) % 12,
                    (root + 7) % 12, (root + 8) % 12, (root + 10) % 12
                ]),
                TonalityType.Minor,
                root
            ));
        }

        return elements;
    }

    private string GetNoteName(int pitchClass)
    {
        var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return noteNames[pitchClass];
    }

    // Additional helper methods would be implemented here...
    private (List<ITonalElement>, List<ITonalElement>) SplitElementsByTonalPartition(
        List<ITonalElement> elements, TonalPartitionPlane partition)
    {
        return ([], []);
    }

    private TonalRegion CreateChildRegion(TonalRegion parent, TonalPartitionPlane partition, bool isLeft)
    {
        return parent;
    }

    private double CalculateTonalCoherence(List<ITonalElement> elements)
    {
        return 1.0;
    }

    private double CalculateTonalSeparation(List<ITonalElement> left, List<ITonalElement> right)
    {
        return 1.0;
    }

    private bool RegionIntersects(TonalRegion a, TonalRegion b)
    {
        return true;
    }

    private double CalculateCircleOfFifthsDistance(Vector3 coords, TonalPartitionPlane plane)
    {
        return 0.0;
    }

    private double CalculateChromaticDistance(Vector3 coords, TonalPartitionPlane plane)
    {
        return 0.0;
    }

    private double CalculateHarmonicDistance(Vector3 coords, TonalPartitionPlane plane)
    {
        return 0.0;
    }

    private double CalculateModalBrightnessDistance(Vector3 coords, TonalPartitionPlane plane)
    {
        return 0.0;
    }

    private double CalculateTonalStabilityDistance(Vector3 coords, TonalPartitionPlane plane)
    {
        return 0.0;
    }
}
