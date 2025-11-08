namespace GA.Business.Core.Tonal.Primitives;

/// <summary>
///     Interface for scale degree naming functionality
/// </summary>
/// <remarks>
///     Provides methods to get the name and short name of a scale degree
/// </remarks>
[PublicAPI]
public interface IScaleDegreeNaming
{
    /// <summary>
    ///     Gets the full name of the scale degree (e.g., "Ionian", "Dorian")
    /// </summary>
    /// <returns>The full name as a string</returns>
    string ToName();

    /// <summary>
    ///     Gets the short name of the scale degree (e.g., "I", "II")
    /// </summary>
    /// <returns>The short name as a string</returns>
    string ToShortName();
}
