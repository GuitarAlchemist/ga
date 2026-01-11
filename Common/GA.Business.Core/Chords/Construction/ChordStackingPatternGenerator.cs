namespace GA.Business.Core.Chords.Construction;

using System;
using System.Collections.Generic;
using System.Linq;
using Chords;

/// <summary>
///     Service responsible for generating interval stacking patterns for chord construction.
///     Supports tertian (thirds), quartal (fourths), and quintal (fifths) stacking approaches.
/// </summary>
public static class ChordStackingPatternGenerator
{
    /// <summary>
    ///     Generates stacking positions for chord construction using different interval stacking approaches.
    ///     This programmatically calculates the scale degree offsets needed for each chord tone.
    /// </summary>
    public static int[] GenerateStackingPositions(ChordExtension extension, int scaleLength,
        ChordStackingType stackingType)
    {
        return stackingType switch
        {
            ChordStackingType.Tertian => GenerateTertianStackingPositions(extension, scaleLength),
            ChordStackingType.Quartal => GenerateQuartalStackingPositions(extension, scaleLength),
            ChordStackingType.Quintal => GenerateQuintalStackingPositions(extension, scaleLength),
            _ => GenerateTertianStackingPositions(extension, scaleLength) // Default to tertian
        };
    }

    #region Tertian (Third-based) Stacking

    /// <summary>
    ///     Generates stacking positions for traditional tertian (third-based) chord construction
    /// </summary>
    private static int[] GenerateTertianStackingPositions(ChordExtension extension, int scaleLength)
    {
        return extension switch
        {
            ChordExtension.Triad => GenerateIntervalStackingPattern(2, 2), // Root + 2 more thirds (3rd, 5th)
            ChordExtension.Seventh => GenerateIntervalStackingPattern(2, 3), // Root + 3 more thirds (3rd, 5th, 7th)
            ChordExtension.Ninth => GenerateExtendedStackingPattern(2, 4, scaleLength), // Add 9th (2nd + octave)
            ChordExtension.Eleventh => GenerateExtendedStackingPattern(2, 5, scaleLength), // Add 11th (4th + octave)
            ChordExtension.Thirteenth => GenerateExtendedStackingPattern(2, 6, scaleLength), // Add 13th (6th + octave)
            _ => GenerateIntervalStackingPattern(2, 2) // Default to triad
        };
    }

    #endregion

    #region Quartal (Fourth-based) Stacking

    /// <summary>
    ///     Generates stacking positions for quartal (fourth-based) chord construction.
    ///     Quartal harmony is common in modern jazz and contemporary classical music.
    /// </summary>
    private static int[] GenerateQuartalStackingPositions(ChordExtension extension, int scaleLength)
    {
        return extension switch
        {
            ChordExtension.Triad => GenerateIntervalStackingPattern(3, 2), // Root + 2 more fourths (4th, 7th)
            ChordExtension.Seventh => GenerateIntervalStackingPattern(3,
                3), // Root + 3 more fourths (4th, 7th, 3rd+octave)
            ChordExtension.Ninth => GenerateExtendedStackingPattern(3, 4, scaleLength), // Add more fourths
            ChordExtension.Eleventh => GenerateExtendedStackingPattern(3, 5, scaleLength),
            ChordExtension.Thirteenth => GenerateExtendedStackingPattern(3, 6, scaleLength),
            _ => GenerateIntervalStackingPattern(3, 2) // Default to triad
        };
    }

    #endregion

    #region Quintal (Fifth-based) Stacking

    /// <summary>
    ///     Generates stacking positions for quintal (fifth-based) chord construction.
    ///     Quintal harmony creates open, spacious sounds often used in contemporary music.
    /// </summary>
    private static int[] GenerateQuintalStackingPositions(ChordExtension extension, int scaleLength)
    {
        return extension switch
        {
            ChordExtension.Triad => GenerateIntervalStackingPattern(4, 2), // Root + 2 more fifths (5th, 2nd+octave)
            ChordExtension.Seventh => GenerateIntervalStackingPattern(4,
                3), // Root + 3 more fifths (5th, 2nd+octave, 6th+octave)
            ChordExtension.Ninth => GenerateExtendedStackingPattern(4, 4, scaleLength), // Add more fifths
            ChordExtension.Eleventh => GenerateExtendedStackingPattern(4, 5, scaleLength),
            ChordExtension.Thirteenth => GenerateExtendedStackingPattern(4, 6, scaleLength),
            _ => GenerateIntervalStackingPattern(4, 2) // Default to triad
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Generates positions by stacking a specific interval within one octave
    /// </summary>
    /// <param name="intervalStep">Scale degree step size (2=thirds, 3=fourths, 4=fifths)</param>
    /// <param name="numberOfIntervals">Number of intervals to stack</param>
    private static int[] GenerateIntervalStackingPattern(int intervalStep, int numberOfIntervals)
    {
        return [.. Enumerable.Range(1, numberOfIntervals).Select(i => i * intervalStep)];
    }

    /// <summary>
    ///     Generates positions for extended chords that go beyond the basic structure.
    ///     Extended intervals wrap around the scale and are represented as their simple equivalents.
    /// </summary>
    /// <param name="intervalStep">Scale degree step size (2=thirds, 3=fourths, 4=fifths)</param>
    /// <param name="totalIntervals">Total number of intervals including extensions</param>
    /// <param name="scaleLength">Number of notes in the parent scale</param>
    private static int[] GenerateExtendedStackingPattern(int intervalStep, int totalIntervals, int scaleLength)
    {
        var positions = new List<int>();

        // First, add the basic chord structure (within octave)
        var basicCount = Math.Min(3, totalIntervals); // Basic 3-note structure or less
        var basicPositions = GenerateIntervalStackingPattern(intervalStep, basicCount);
        positions.AddRange(basicPositions);

        // Then add extensions (beyond octave, represented as simple intervals)
        for (var i = basicCount + 1; i <= totalIntervals; i++)
        {
            // Calculate the scale degree for this extension
            var extensionDegree = (i - 1) * intervalStep % scaleLength;
            if (extensionDegree == 0)
            {
                extensionDegree = scaleLength; // Handle wrap-around
            }

            positions.Add(extensionDegree);
        }

        return [.. positions];
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    ///     Gets the interval step size for a given stacking type
    /// </summary>
    public static int GetIntervalStepSize(ChordStackingType stackingType)
    {
        return stackingType switch
        {
            ChordStackingType.Tertian => 2, // Thirds
            ChordStackingType.Quartal => 3, // Fourths
            ChordStackingType.Quintal => 4, // Fifths
            _ => 2 // Default to thirds
        };
    }

    /// <summary>
    ///     Gets a human-readable description of the stacking type
    /// </summary>
    public static string GetStackingDescription(ChordStackingType stackingType)
    {
        return stackingType switch
        {
            ChordStackingType.Tertian => "Stacked thirds (traditional harmony)",
            ChordStackingType.Quartal => "Stacked fourths (modern jazz harmony)",
            ChordStackingType.Quintal => "Stacked fifths (contemporary harmony)",
            _ => "Unknown stacking type"
        };
    }

    #endregion
}
