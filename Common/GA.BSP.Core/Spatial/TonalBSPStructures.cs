namespace GA.BSP.Core.Spatial;

using System.Numerics;
using Business.Core.Atonal;

/// <summary>
///     Node in the Tonal Binary Space Partitioning tree
/// </summary>
public class TonalBspNode
{
    public TonalRegion Region { get; set; } = new();
    public TonalPartitionPlane? PartitionPlane { get; set; }
    public TonalBspNode? Left { get; set; }
    public TonalBspNode? Right { get; set; }
    public List<ITonalElement> Elements { get; set; } = [];
    public bool IsLeaf { get; set; }

    /// <summary>
    ///     Get all elements in this subtree
    /// </summary>
    public IEnumerable<ITonalElement> GetAllElements()
    {
        if (IsLeaf)
        {
            return Elements;
        }

        var allElements = new List<ITonalElement>();
        if (Left != null)
        {
            allElements.AddRange(Left.GetAllElements());
        }

        if (Right != null)
        {
            allElements.AddRange(Right.GetAllElements());
        }

        return allElements;
    }

    /// <summary>
    ///     Get the depth of this subtree
    /// </summary>
    public int GetDepth()
    {
        if (IsLeaf)
        {
            return 1;
        }

        var leftDepth = Left?.GetDepth() ?? 0;
        var rightDepth = Right?.GetDepth() ?? 0;

        return 1 + Math.Max(leftDepth, rightDepth);
    }
}

/// <summary>
///     Represents a region in tonal space
/// </summary>
public record TonalRegion
{
    public TonalRegion()
    {
    }

    public TonalRegion(string name, TonalityType tonalityType, PitchClassSet pitchClassSet, double tonalCenter)
    {
        Name = name;
        TonalityType = tonalityType;
        PitchClassSet = pitchClassSet;
        TonalCenter = tonalCenter;
    }

    public string Name { get; init; } = "";
    public TonalityType TonalityType { get; init; }
    public PitchClassSet PitchClassSet { get; init; } = new([]);
    public double TonalCenter { get; init; }
    public double TonalStrength { get; init; } = 1.0;
    public Vector3 Bounds { get; init; } = Vector3.One;

    /// <summary>
    ///     Check if this region contains a given pitch class set
    /// </summary>
    public bool Contains(PitchClassSet pitchClassSet)
    {
        return TonalityType switch
        {
            TonalityType.Major => ContainsMajorTonality(pitchClassSet),
            TonalityType.Minor => ContainsMinorTonality(pitchClassSet),
            TonalityType.Modal => ContainsModalTonality(pitchClassSet),
            TonalityType.Atonal => true, // Atonal regions contain everything
            _ => false
        };
    }

    private bool ContainsMajorTonality(PitchClassSet pitchClassSet)
    {
        // Check if the pitch class set fits within major tonality
        return PitchClassSet.IsSubsetOf(pitchClassSet) || pitchClassSet.IsSubsetOf(PitchClassSet);
    }

    private bool ContainsMinorTonality(PitchClassSet pitchClassSet)
    {
        // Check if the pitch class set fits within minor tonality
        return PitchClassSet.IsSubsetOf(pitchClassSet) || pitchClassSet.IsSubsetOf(PitchClassSet);
    }

    private bool ContainsModalTonality(PitchClassSet pitchClassSet)
    {
        // Check if the pitch class set fits within modal tonality
        return PitchClassSet.IsSubsetOf(pitchClassSet) || pitchClassSet.IsSubsetOf(PitchClassSet);
    }
}

/// <summary>
///     Partition plane for dividing tonal space
/// </summary>
public record TonalPartitionPlane
{
    public TonalPartitionStrategy Strategy { get; init; }
    public double ReferencePoint { get; init; }
    public Vector3 Normal { get; init; }
    public double Threshold { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    ///     Evaluate which side of the partition a tonal element falls on
    /// </summary>
    public int EvaluateSide(ITonalElement element)
    {
        var distance = CalculateDistance(element);
        return distance <= Threshold ? -1 : 1; // -1 for left, 1 for right
    }

    private double CalculateDistance(ITonalElement element)
    {
        return Strategy switch
        {
            TonalPartitionStrategy.CircleOfFifths => CalculateCircleOfFifthsDistance(element),
            TonalPartitionStrategy.ChromaticDistance => CalculateChromaticDistance(element),
            TonalPartitionStrategy.HarmonicSeries => CalculateHarmonicDistance(element),
            TonalPartitionStrategy.ModalBrightness => CalculateModalBrightnessDistance(element),
            TonalPartitionStrategy.TonalStability => CalculateTonalStabilityDistance(element),
            _ => 0.0
        };
    }

    private double CalculateCircleOfFifthsDistance(ITonalElement element)
    {
        // Distance along the circle of fifths
        var elementFifthsPosition = element.TonalCenter * 7 % 12;
        var referenceFifthsPosition = ReferencePoint * 7 % 12;

        var distance = Math.Abs(elementFifthsPosition - referenceFifthsPosition);
        return Math.Min(distance, 12 - distance); // Circular distance
    }

    private double CalculateChromaticDistance(ITonalElement element)
    {
        // Simple chromatic distance
        var distance = Math.Abs(element.TonalCenter - ReferencePoint);
        return Math.Min(distance, 12 - distance); // Circular distance
    }

    private double CalculateHarmonicDistance(ITonalElement element)
    {
        // Distance based on harmonic series relationships
        var elementHarmonic = Math.Log2((element.TonalCenter + 1) / 1.0);
        var referenceHarmonic = Math.Log2((ReferencePoint + 1) / 1.0);

        return Math.Abs(elementHarmonic - referenceHarmonic);
    }

    private double CalculateModalBrightnessDistance(ITonalElement element)
    {
        // Distance based on modal brightness (number of sharps/flats)
        var elementBrightness = CalculateBrightness(element.PitchClassSet);
        var referenceBrightness = ReferencePoint;

        return Math.Abs(elementBrightness - referenceBrightness);
    }

    private double CalculateTonalStabilityDistance(ITonalElement element)
    {
        // Distance based on tonal stability (consonance/dissonance)
        var elementStability = CalculateStability(element.PitchClassSet);
        var referenceStability = ReferencePoint;

        return Math.Abs(elementStability - referenceStability);
    }

    private double CalculateBrightness(PitchClassSet pitchClassSet)
    {
        // Calculate modal brightness based on interval content
        var intervals = pitchClassSet.IntervalClassVector;

        // Major thirds and perfect fifths contribute to brightness
        var brightness = intervals[3] * 0.5 + intervals[4] * 0.3; // IC3 = major third, IC4 = perfect fourth

        return brightness / pitchClassSet.Cardinality.Value;
    }

    private double CalculateStability(PitchClassSet pitchClassSet)
    {
        // Calculate tonal stability based on consonant intervals
        var intervals = pitchClassSet.IntervalClassVector;

        // Perfect consonances contribute to stability
        var stability = intervals[0] * 0.1 + // Unison/octave
                        intervals[4] * 0.4 + // Perfect fourth/fifth
                        intervals[3] * 0.3 + // Major/minor third
                        intervals[5] * 0.2; // Perfect fifth

        return stability / pitchClassSet.Cardinality.Value;
    }
}

/// <summary>
///     Strategies for partitioning tonal space
/// </summary>
public enum TonalPartitionStrategy
{
    CircleOfFifths, // Partition based on circle of fifths relationships
    ChromaticDistance, // Partition based on chromatic distance
    HarmonicSeries, // Partition based on harmonic series relationships
    ModalBrightness, // Partition based on modal brightness (sharp/flat tendency)
    TonalStability // Partition based on consonance/dissonance
}

/// <summary>
///     Types of tonality for regions
/// </summary>
public enum TonalityType
{
    Major,
    Minor,
    Modal,
    Atonal,
    Chromatic,
    Pentatonic,
    Blues,
    WholeTone,
    Diminished
}

/// <summary>
///     Interface for elements that can be placed in tonal space
/// </summary>
public interface ITonalElement
{
    string Name { get; }
    PitchClassSet PitchClassSet { get; }
    TonalityType TonalityType { get; }
    double TonalCenter { get; }
    double TonalStrength { get; }
}

/// <summary>
///     Concrete implementation of a tonal scale
/// </summary>
public record TonalScale : ITonalElement
{
    public TonalScale()
    {
    }

    public TonalScale(string name, PitchClassSet pitchClassSet, TonalityType tonalityType, double tonalCenter)
    {
        Name = name;
        PitchClassSet = pitchClassSet;
        TonalityType = tonalityType;
        TonalCenter = tonalCenter;
    }

    public string Name { get; init; } = "";
    public PitchClassSet PitchClassSet { get; init; } = new([]);
    public TonalityType TonalityType { get; init; }
    public double TonalCenter { get; init; }
    public double TonalStrength { get; init; } = 1.0;
}

/// <summary>
///     Concrete implementation of a tonal chord
/// </summary>
public record TonalChord : ITonalElement
{
    public TonalChord()
    {
    }

    public TonalChord(string name, PitchClassSet pitchClassSet, TonalityType tonalityType,
        double tonalCenter, string function = "")
    {
        Name = name;
        PitchClassSet = pitchClassSet;
        TonalityType = tonalityType;
        TonalCenter = tonalCenter;
        Function = function;
    }

    public string Function { get; init; } = ""; // Tonic, Subdominant, Dominant, etc.
    public string Name { get; init; } = "";
    public PitchClassSet PitchClassSet { get; init; } = new([]);
    public TonalityType TonalityType { get; init; }
    public double TonalCenter { get; init; }
    public double TonalStrength { get; init; } = 1.0;
}

/// <summary>
///     Query result from BSP tree
/// </summary>
public record TonalBspQueryResult
{
    public TonalBspQueryResult()
    {
    }

    public TonalBspQueryResult(TonalRegion region, List<ITonalElement> elements,
        double confidence, int depth, TimeSpan queryTime)
    {
        Region = region;
        Elements = elements;
        Confidence = confidence;
        Depth = depth;
        QueryTime = queryTime;
    }

    public TonalRegion Region { get; init; } = new();
    public List<ITonalElement> Elements { get; init; } = [];
    public double Confidence { get; init; }
    public int Depth { get; init; }
    public TimeSpan QueryTime { get; init; }
}
