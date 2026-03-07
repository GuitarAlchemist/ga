namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Represents the harmonic function of an interval within a chord
///     (<see href="https://en.wikipedia.org/wiki/Function_(music)" />)
/// </summary>
/// <remarks>
/// Expanded to include 9th, 11th, and 13th for jazz and extended harmony.
/// See also: <see href="https://en.wikipedia.org/wiki/Extended_chord" /> for 9th/11th/13th.
/// </remarks>
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
