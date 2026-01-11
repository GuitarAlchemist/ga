namespace GA.Business.Core.Fretboard.Biomechanics;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;

/// <summary>
///     Represents a target chord configuration for inverse kinematics solving
/// </summary>
public record ChordTarget
{
    /// <summary>
    ///     Name of the chord (e.g., "C Major", "F# Minor")
    /// </summary>
    public required string ChordName { get; init; }

    /// <summary>
    ///     Fret positions for the chord (string, fret pairs)
    /// </summary>
    public required ImmutableArray<(int String, int Fret)> FretPositions { get; init; }

    /// <summary>
    ///     Target 3D positions for each finger
    /// </summary>
    public required ImmutableDictionary<FingerType, Vector3> TargetPositions { get; init; }

    /// <summary>
    ///     Approach directions for each finger (normalized vectors)
    /// </summary>
    public required ImmutableDictionary<FingerType, Vector3> ApproachDirections { get; init; }

    /// <summary>
    ///     Tolerance for finger positioning (in millimeters)
    /// </summary>
    public required float Tolerance { get; init; }

    /// <summary>
    ///     Barre coverage - which strings each finger covers for barre chords
    /// </summary>
    public required ImmutableDictionary<FingerType, ImmutableArray<int>> BarreCoverage { get; init; }

    /// <summary>
    ///     Create a simple chord target with basic finger assignments
    /// </summary>
    public static ChordTarget Create(
        string chordName,
        IReadOnlyDictionary<FingerType, (int String, int Fret)> assignments,
        float tolerance = 5.0f)
    {
        var fretPositions = assignments.Values.ToImmutableArray();
        var targetPositions = ImmutableDictionary.CreateBuilder<FingerType, Vector3>();
        var approachDirections = ImmutableDictionary.CreateBuilder<FingerType, Vector3>();
        var barreCoverage = ImmutableDictionary.CreateBuilder<FingerType, ImmutableArray<int>>();

        foreach (var (finger, assignment) in assignments)
        {
            // Calculate 3D position from string/fret coordinates
            targetPositions[finger] = CalculateFretPosition(assignment.String, assignment.Fret);

            // Default approach direction (thumb from behind, others from front)
            approachDirections[finger] = finger == FingerType.Thumb
                ? new(0f, 0f, 1f)
                : new Vector3(0f, 0f, -1f);

            // Single string coverage by default
            barreCoverage[finger] = [assignment.String];
        }

        return new()
        {
            ChordName = chordName,
            FretPositions = fretPositions,
            TargetPositions = targetPositions.ToImmutable(),
            ApproachDirections = approachDirections.ToImmutable(),
            Tolerance = tolerance,
            BarreCoverage = barreCoverage.ToImmutable()
        };
    }

    /// <summary>
    ///     Calculate 3D position from string and fret coordinates
    /// </summary>
    private static Vector3 CalculateFretPosition(int stringNumber, int fret)
    {
        // Standard guitar dimensions (approximate)
        const float stringSpacing = 10.5f; // mm between strings
        const float fretSpacing = 35.0f; // mm between frets (varies, this is average)
        const float nutHeight = 3.0f; // mm above fretboard

        // String 1 (high E) is at the bottom, string 6 (low E) at top
        var x = (6 - stringNumber) * stringSpacing; // X position across strings
        var y = fret * fretSpacing; // Y position along neck
        var z = nutHeight; // Z height above fretboard

        return new(x, y, z);
    }
}
