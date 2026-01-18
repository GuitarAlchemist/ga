namespace GA.Business.Core.Progressions;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a named sequence of chords/voicings.
/// Used for playback, analysis, and educational examples.
/// </summary>
public class Progression
{
    /// <summary>
    /// Unique Identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name (e.g., "ii-V-I in C Major").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description or theory notes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The sequence of chords.
    /// </summary>
    public List<ProgressionStep> Steps { get; set; } = new();

    /// <summary>
    /// Tags for categorization (e.g., "jazz", "beginner", "cadence").
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
