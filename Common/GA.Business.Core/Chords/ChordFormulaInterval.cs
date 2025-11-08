namespace GA.Business.Core.Chords;

using Intervals;

/// <summary>
///     Represents an interval within a chord formula with additional chord-specific properties
/// </summary>
public class ChordFormulaInterval
{
    /// <summary>
    ///     Initializes a new instance of the ChordFormulaInterval class
    /// </summary>
    public ChordFormulaInterval(
        Interval interval,
        ChordFunction function,
        bool isEssential = true,
        bool isTypicallyDoubled = false,
        VoiceLeadingTendency voiceLeadingTendency = VoiceLeadingTendency.Stable)
    {
        Interval = interval;
        Function = function;
        IsEssential = isEssential;
        IsTypicallyDoubled = isTypicallyDoubled;
        VoiceLeadingTendency = voiceLeadingTendency;
    }

    /// <summary>
    ///     Gets whether this interval is essential to the chord's identity
    /// </summary>
    public bool IsEssential { get; }

    /// <summary>
    ///     Gets whether this interval can be omitted in certain voicings
    /// </summary>
    public bool IsOptional => !IsEssential;

    /// <summary>
    ///     Gets the chord function of this interval (root, third, fifth, etc.)
    /// </summary>
    public ChordFunction Function { get; }

    /// <summary>
    ///     Gets whether this interval is typically doubled in voicings
    /// </summary>
    public bool IsTypicallyDoubled { get; }

    /// <summary>
    ///     Gets the voice leading tendency of this interval
    /// </summary>
    public VoiceLeadingTendency VoiceLeadingTendency { get; }

    /// <summary>
    ///     Gets the interval
    /// </summary>
    public Interval Interval { get; }

    /// <summary>
    ///     Gets the chord degree number (1, 3, 5, 7, 9, 11, 13)
    /// </summary>
    public int ChordDegree => Function switch
    {
        ChordFunction.Root => 1,
        ChordFunction.Third => 3,
        ChordFunction.Fifth => 5,
        ChordFunction.Seventh => 7,
        ChordFunction.Ninth => 9,
        ChordFunction.Eleventh => 11,
        ChordFunction.Thirteenth => 13,
        _ => throw new InvalidOperationException($"Unknown chord function: {Function}")
    };

    /// <summary>
    ///     Returns a string representation of the chord formula interval
    /// </summary>
    public override string ToString()
    {
        var essential = IsEssential ? "" : " (optional)";
        var doubled = IsTypicallyDoubled ? " (doubled)" : "";
        return $"{Function}: {Interval}{essential}{doubled}";
    }
}

/// <summary>
///     Represents the harmonic function of an interval within a chord
/// </summary>
public enum ChordFunction
{
    Root,
    Third,
    Fifth,
    Seventh,
    Ninth,
    Eleventh,
    Thirteenth
}

/// <summary>
///     Represents the voice leading tendency of a chord tone
/// </summary>
public enum VoiceLeadingTendency
{
    /// <summary>
    ///     Stable tone that tends to remain stationary
    /// </summary>
    Stable,

    /// <summary>
    ///     Active tone that tends to resolve upward
    /// </summary>
    ResolvesUp,

    /// <summary>
    ///     Active tone that tends to resolve downward
    /// </summary>
    ResolvesDown,

    /// <summary>
    ///     Flexible tone that can move in either direction
    /// </summary>
    Flexible
}
