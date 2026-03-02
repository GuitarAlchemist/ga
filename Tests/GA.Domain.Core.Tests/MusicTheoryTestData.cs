namespace GA.Domain.Core.Tests;

using GA.Domain.Core.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using GA.Domain.Core.Theory.Harmony;

/// <summary>
/// Helper class providing common test data for domain tests.
/// </summary>
public static class MusicTheoryTestData
{
    /// <summary>
    /// Common pitch class values for testing.
    /// </summary>
    public static readonly PitchClass[] CommonPitchClasses =
    [
        PitchClass.C, PitchClass.D, PitchClass.E, 
        PitchClass.F, PitchClass.G, PitchClass.A, PitchClass.B
    ];

    /// <summary>
    /// Common accidentals for testing.
    /// </summary>
    public static readonly PitchClass[] Accidentals =
    [
        PitchClass.CSharp, PitchClass.DSharp, PitchClass.FSharp, 
        PitchClass.GSharp, PitchClass.ASharp
    ];

    /// <summary>
    /// Major scale intervals for testing.
    /// </summary>
    public static readonly int[] MajorScaleIntervals = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>
    /// Minor scale intervals for testing.
    /// </summary>
    public static readonly int[] MinorScaleIntervals = [0, 2, 3, 5, 7, 8, 10];

    /// <summary>
    /// Common chord intervals for testing.
    /// </summary>
    public static readonly int[][] CommonChordIntervals =
    [
        [0, 4, 7],     // Major
        [0, 3, 7],     // Minor  
        [0, 4, 7, 11], // Major 7th
        [0, 3, 7, 10], // Minor 7th
        [0, 4, 7, 10]  // Dominant 7th
    ];

    /// <summary>
    /// Creates a test scale with the specified pitch classes.
    /// </summary>
    public static Scale CreateTestScale(params PitchClass[] pitchClasses)
    {
        var notes = pitchClasses.Select(pc => pc.ToSharpNote().ToAccidented());
        return new Scale(notes);
    }

    /// <summary>
    /// Creates a test chord template with the specified intervals.
    /// </summary>
    public static ChordTemplate CreateTestChord(string name, params int[] semitoneIntervals)
    {
        var formulaIntervals = semitoneIntervals
            .Select(s => new Interval.Chromatic(Semitones.FromValue(s)))
            .Select(i => new ChordFormulaInterval(i, ChordFunction.Root))
            .ToList();

        var formula = new ChordFormula(name, formulaIntervals);
        var pcs = new PitchClassSet(formulaIntervals.Select(i => PitchClass.FromValue(i.Interval.Semitones.Value)));
        return new ChordTemplate.Analytical(pcs, formula, "Test Chord");
    }
}