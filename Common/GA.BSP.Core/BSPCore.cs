namespace GA.BSP.Core;

using System.Diagnostics;

/// <summary>
///     Simple pitch class enumeration for BSP demo
/// </summary>
public enum BspPitchClass
{
    C = 0,
    CSharp = 1,
    D = 2,
    DSharp = 3,
    E = 4,
    F = 5,
    FSharp = 6,
    G = 7,
    GSharp = 8,
    A = 9,
    ASharp = 10,
    B = 11
}

/// <summary>
///     Simple pitch class set for BSP operations
/// </summary>
public class BspPitchClassSet(IEnumerable<BspPitchClass> pitchClasses) : HashSet<BspPitchClass>(pitchClasses)
{
    public override string ToString()
    {
        return string.Join(", ", this.OrderBy(pc => (int)pc));
    }
}

/// <summary>
///     Tonality types for musical analysis
/// </summary>
public enum TonalityType
{
    Major,
    Minor,
    Diminished,
    Augmented,
    Chromatic,
    Atonal
}

/// <summary>
///     BSP partition strategies
/// </summary>
public enum TonalPartitionStrategy
{
    CircleOfFifths,
    ChromaticDistance,
    SetComplexity,
    TonalHierarchy
}

/// <summary>
///     Tonal region in BSP space
/// </summary>
public class TonalRegion
{
    public TonalRegion()
    {
    }

    public TonalRegion(string name, TonalityType tonalityType, BspPitchClassSet pitchClassSet, int tonalCenter)
    {
        Name = name;
        TonalityType = tonalityType;
        PitchClassSet = pitchClassSet;
        TonalCenter = tonalCenter;
    }

    public string Name { get; init; } = "";
    public TonalityType TonalityType { get; init; }
    public BspPitchClassSet PitchClassSet { get; init; } = new([]);
    public int TonalCenter { get; init; }

    /// <summary>
    ///     Check if this region contains the given pitch class set
    /// </summary>
    public bool Contains(GA.Domain.Core.Theory.Atonal.PitchClassSet pitchClassSet)
    {
        return pitchClassSet.All(pc => PitchClassSet.Contains((BspPitchClass)pc.Value));
    }
}

/// <summary>
///     BSP node for tonal space partitioning
/// </summary>
public class TonalBspNode
{
    public TonalBspNode()
    {
    }

    public TonalBspNode(TonalRegion region)
    {
        Region = region;
    }

    public TonalRegion Region { get; init; } = new();
    public TonalBspNode? Left { get; set; }
    public TonalBspNode? Right { get; set; }
    public bool IsLeaf => Left == null && Right == null;
}

/// <summary>
///     Simple BSP tree for tonal space
/// </summary>
public class TonalBspTree
{
    public TonalBspTree()
    {
        // Create a default root region (chromatic space)
        var chromaticSpace = new BspPitchClassSet(Enum.GetValues<BspPitchClass>());
        Root = new TonalBspNode(new TonalRegion("Chromatic Space", TonalityType.Chromatic, chromaticSpace, 0));

        // Add some basic partitions
        InitializeBasicPartitions();
    }

    public TonalBspNode Root { get; }

    private void InitializeBasicPartitions()
    {
        // Create major and minor regions
        var majorRegion = new TonalRegion(
            "Major Regions",
            TonalityType.Major,
            new BspPitchClassSet([
                BspPitchClass.C, BspPitchClass.D, BspPitchClass.E, BspPitchClass.F, BspPitchClass.G, BspPitchClass.A, BspPitchClass.B
            ]),
            (int)BspPitchClass.C
        );

        var minorRegion = new TonalRegion(
            "Minor Regions",
            TonalityType.Minor,
            new BspPitchClassSet([
                BspPitchClass.A, BspPitchClass.B, BspPitchClass.C, BspPitchClass.D, BspPitchClass.E, BspPitchClass.F, BspPitchClass.G
            ]),
            (int)BspPitchClass.A
        );

        Root.Left = new TonalBspNode(majorRegion);
        Root.Right = new TonalBspNode(minorRegion);
    }

    /// <summary>
    ///     Find the best tonal region for a given pitch class set
    /// </summary>
    public TonalRegion FindTonalRegion(GA.Domain.Core.Theory.Atonal.PitchClassSet pitchClassSet)
    {
        return FindTonalRegionRecursive(Root, pitchClassSet);
    }

    private TonalRegion FindTonalRegionRecursive(TonalBspNode node, GA.Domain.Core.Theory.Atonal.PitchClassSet pitchClassSet)
    {
        if (node.IsLeaf)
        {
            return node.Region;
        }

        // Simple heuristic: choose the child that contains more of the pitch classes
        var leftFit = node.Left != null ? CalculateFit(node.Left.Region, pitchClassSet) : 0;
        var rightFit = node.Right != null ? CalculateFit(node.Right.Region, pitchClassSet) : 0;

        if (leftFit >= rightFit && node.Left != null)
        {
            return FindTonalRegionRecursive(node.Left, pitchClassSet);
        }

        if (node.Right != null)
        {
            return FindTonalRegionRecursive(node.Right, pitchClassSet);
        }

        return node.Region;
    }

    private int CalculateFit(TonalRegion region, GA.Domain.Core.Theory.Atonal.PitchClassSet pitchClassSet)
    {
        return pitchClassSet.Count(pc => region.PitchClassSet.Contains((BspPitchClass)pc.Value));
    }
}

/// <summary>
///     Tonal element interface
/// </summary>
public interface ITonalElement
{
    string Name { get; }
    BspPitchClassSet PitchClassSet { get; }
    TonalityType TonalityType { get; }
    double TonalCenter { get; }
}

/// <summary>
///     Simple tonal chord implementation
/// </summary>
public class TonalChord : ITonalElement
{
    public TonalChord()
    {
    }

    public TonalChord(string name, BspPitchClassSet pitchClassSet, TonalityType tonalityType, double tonalCenter)
    {
        Name = name;
        PitchClassSet = pitchClassSet;
        TonalityType = tonalityType;
        TonalCenter = tonalCenter;
    }

    public double TonalStrength { get; init; } = 1.0;
    public string Name { get; init; } = "";
    public BspPitchClassSet PitchClassSet { get; init; } = new([]);
    public TonalityType TonalityType { get; init; }
    public double TonalCenter { get; init; }
}

/// <summary>
///     Simple tonal scale implementation
/// </summary>
public class TonalScale : ITonalElement
{
    public TonalScale()
    {
    }

    public TonalScale(string name, BspPitchClassSet pitchClassSet, TonalityType tonalityType, double tonalCenter)
    {
        Name = name;
        PitchClassSet = pitchClassSet;
        TonalityType = tonalityType;
        TonalCenter = tonalCenter;
    }

    public double TonalStrength { get; init; } = 1.0;
    public string Name { get; init; } = "";
    public BspPitchClassSet PitchClassSet { get; init; } = new([]);
    public TonalityType TonalityType { get; init; }
    public double TonalCenter { get; init; }
}

/// <summary>
///     BSP query result
/// </summary>
public class TonalBspQueryResult
{
    public TonalBspQueryResult()
    {
    }

    public TonalBspQueryResult(TonalRegion region, List<ITonalElement> elements, double confidence, TimeSpan queryTime)
    {
        Region = region;
        Elements = elements;
        Confidence = confidence;
        QueryTime = queryTime;
    }

    public TonalRegion Region { get; init; } = new();
    public List<ITonalElement> Elements { get; init; } = new();
    public double Confidence { get; init; }
    public TimeSpan QueryTime { get; init; }
}

/// <summary>
///     Simple BSP service for tonal analysis
/// </summary>
public class TonalBspService
{
    private readonly TonalBspTree _tree = new();

    /// <summary>
    ///     Perform spatial query for similar elements
    /// </summary>
    public TonalBspQueryResult SpatialQuery(GA.Domain.Core.Theory.Atonal.PitchClassSet center, double radius, TonalPartitionStrategy strategy)
    {
        var stopwatch = Stopwatch.StartNew();

        var region = _tree.FindTonalRegion(center);
        var bspSet = new BspPitchClassSet(center.Select(pc => (BspPitchClass)pc.Value));
        var elements = new List<ITonalElement>
        {
            new TonalChord("Query Result", bspSet, region.TonalityType, region.TonalCenter)
        };
        var confidence = region.Contains(center) ? 0.9 : 0.5;

        stopwatch.Stop();

        return new TonalBspQueryResult(region, elements, confidence, stopwatch.Elapsed);
    }

    /// <summary>
    ///     Find tonal context for a chord
    /// </summary>
    public TonalBspQueryResult FindTonalContextForChord(GA.Domain.Core.Theory.Atonal.PitchClassSet pitchClassSet)
    {
        var stopwatch = Stopwatch.StartNew();

        var region = _tree.FindTonalRegion(pitchClassSet);
        var bspSet = new BspPitchClassSet(pitchClassSet.Select(pc => (BspPitchClass)pc.Value));
        var elements = new List<ITonalElement>
        {
            new TonalChord("Context", bspSet, region.TonalityType, region.TonalCenter)
        };
        var confidence = region.Contains(pitchClassSet) ? 0.9 : 0.5;

        stopwatch.Stop();

        return new TonalBspQueryResult(region, elements, confidence, stopwatch.Elapsed);
    }
}