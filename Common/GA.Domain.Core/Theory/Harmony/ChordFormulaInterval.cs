namespace GA.Domain.Core.Theory.Harmony;

using Interval = Primitives.Intervals.Interval;

/// <summary>
///     Represents an interval within a chord formula with additional chord-specific properties
/// </summary>
/// <remarks>
/// Example: Major third (M3) is essential, typically doubled, and stable in voice leading
/// </remarks>
[PublicAPI]
public class ChordFormulaInterval(
    Interval interval,
    ChordFunction function,
    bool isEssential = true,
    bool isTypicallyDoubled = false,
    VoiceLeadingTendency voiceLeadingTendency = VoiceLeadingTendency.Stable)
{
    /// <summary>
    ///     Gets whether this interval is essential to the chord's identity
    /// </summary>
    public bool IsEssential { get; } = isEssential;

    /// <summary>
    ///     Gets whether this interval can be omitted in certain voicings
    /// </summary>
    public bool IsOptional => !IsEssential;

    /// <summary>
    ///     Gets the chord function of this interval (root, third, fifth, etc.)
    /// </summary>
    public ChordFunction Function { get; } = function;

    /// <summary>
    ///     Gets whether this interval is typically doubled in voicings
    /// </summary>
    public bool IsTypicallyDoubled { get; } = isTypicallyDoubled;

    /// <summary>
    ///     Gets the voice leading tendency of this interval
    /// </summary>
    public VoiceLeadingTendency VoiceLeadingTendency { get; } = voiceLeadingTendency;

    /// <summary>
    ///     Gets the interval
    /// </summary>
    public Interval Interval { get; } = interval;

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
