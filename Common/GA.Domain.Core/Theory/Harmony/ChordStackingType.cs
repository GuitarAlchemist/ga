namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Represents the type of chord stacking pattern
///     (<see href="https://en.wikipedia.org/wiki/Chord_(music)#Types" />)
/// </summary>
/// <remarks>
/// Example: Cmaj7 = tertian, C4/E4/G4/Bb4 = quartal.
///     See also: <see href="https://en.wikipedia.org/wiki/Quartal_and_quintal_harmony" /> for quartal/quintal stacking.
/// </remarks>
public enum ChordStackingType
{
    /// <summary>
    ///     Traditional tertian harmony (stacked thirds)
    /// </summary>
    Tertian,

    /// <summary>
    ///     Quartal harmony (stacked fourths)
    /// </summary>
    Quartal,

    /// <summary>
    ///     Quintal harmony (stacked fifths)
    /// </summary>
    Quintal,

    /// <summary>
    ///     Secundal harmony (stacked seconds)
    /// </summary>
    Secundal
}
