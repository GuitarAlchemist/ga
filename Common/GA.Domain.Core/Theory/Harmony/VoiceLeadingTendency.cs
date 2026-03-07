namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Represents the voice leading tendency of a chord tone
///     (<see href="https://en.wikipedia.org/wiki/Voice_leading" />)
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
