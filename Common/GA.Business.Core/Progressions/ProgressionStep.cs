namespace GA.Business.Core.Progressions;

using GA.Business.Core.Fretboard.Voicings.Core;

/// <summary>
/// A single step in a musical progression, containing the voicing to play and its duration.
/// </summary>
public class ProgressionStep
{
    /// <summary>
    /// The specific voicing shape to play.
    /// </summary>
    public required Voicing Voicing { get; set; }

    /// <summary>
    /// Duration of this step in milliseconds.
    /// Default is 1000ms (1 second).
    /// </summary>
    public int DurationMs { get; set; } = 1000;

    /// <summary>
    /// Functional analysis or role of this step (e.g., "Tonic", "Dominant", "V7").
    /// </summary>
    public string? Function { get; set; }

    /// <summary>
    /// Display label for the chord (e.g., "Cm7", "G7b9").
    /// </summary>
    public string? Label { get; set; }
}
