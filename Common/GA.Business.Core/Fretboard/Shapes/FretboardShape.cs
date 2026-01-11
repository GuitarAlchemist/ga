namespace GA.Business.Core.Fretboard.Shapes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Atonal;
using JetBrains.Annotations;
using Positions;

/// <summary>
///     Represents a playable shape on the fretboard
/// </summary>
[PublicAPI]
public sealed record FretboardShape
{
    /// <summary>
    ///     Unique identifier for this shape (hash of positions)
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Tuning ID this shape is for
    /// </summary>
    public required string TuningId { get; init; }

    /// <summary>
    ///     Pitch-class set represented by this shape
    /// </summary>
    public required PitchClassSet PitchClassSet { get; init; }

    /// <summary>
    ///     Interval-class vector for this shape
    /// </summary>
    public required IntervalClassVector Icv { get; init; }

    /// <summary>
    ///     Positions on the fretboard (string, fret pairs)
    /// </summary>
    public required IReadOnlyList<PositionLocation> Positions { get; init; }

    /// <summary>
    ///     Bitmask of which strings are used (bit 0 = string 1, etc.)
    /// </summary>
    public required int StringMask { get; init; }

    /// <summary>
    ///     Minimum fret number used (excluding open strings)
    /// </summary>
    public required int MinFret { get; init; }

    /// <summary>
    ///     Maximum fret number used
    /// </summary>
    public required int MaxFret { get; init; }

    /// <summary>
    ///     Fret span (max - min)
    /// </summary>
    public int Span => MaxFret - MinFret;

    /// <summary>
    ///     Diagness: 0 = box shape, 1 = diagonal shape
    ///     Measures how much the shape spreads diagonally across strings
    /// </summary>
    public required double Diagness { get; init; }

    /// <summary>
    ///     Ergonomics score (0-1, higher is better)
    ///     Based on finger stretch, span, and hand position
    /// </summary>
    public required double Ergonomics { get; init; }

    /// <summary>
    ///     Number of fingers required
    /// </summary>
    public required int FingerCount { get; init; }

    /// <summary>
    ///     Root position (lowest fret on lowest string)
    /// </summary>
    public PositionLocation? RootPosition { get; init; }

    /// <summary>
    ///     Tags for categorization (e.g., "box", "diagonal", "barre", "open")
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new();

    /// <summary>
    ///     Compute diagness (0 = box, 1 = diagonal)
    /// </summary>
    public static double ComputeDiagness(IReadOnlyList<PositionLocation> positions)
    {
        if (positions.Count < 2)
        {
            return 0;
        }

        var playedPositions = positions.Where(p => !p.IsMuted).ToList();
        if (playedPositions.Count < 2)
        {
            return 0;
        }

        // Calculate average fret change per string change
        var totalFretChange = 0.0;
        var stringChanges = 0;

        for (var i = 1; i < playedPositions.Count; i++)
        {
            var prev = playedPositions[i - 1];
            var curr = playedPositions[i];

            var stringDiff = Math.Abs(curr.Str.Value - prev.Str.Value);
            if (stringDiff > 0)
            {
                var fretDiff = Math.Abs(curr.Fret.Value - prev.Fret.Value);
                totalFretChange += fretDiff;
                stringChanges++;
            }
        }

        if (stringChanges == 0)
        {
            return 0;
        }

        var avgFretChange = totalFretChange / stringChanges;

        // Normalize: 0 fret change = 0 (box), 4+ fret change = 1 (diagonal)
        return Math.Min(1.0, avgFretChange / 4.0);
    }

    /// <summary>
    ///     Compute ergonomics score (0-1, higher is better)
    /// </summary>
    public static double ComputeErgonomics(IReadOnlyList<PositionLocation> positions, int span)
    {
        if (positions.Count == 0)
        {
            return 0;
        }

        var playedPositions = positions.Where(p => !p.IsMuted && !p.IsOpen).ToList();
        if (playedPositions.Count == 0)
        {
            return 1.0; // Open strings are easy
        }

        // Penalize large spans
        var spanPenalty = Math.Max(0, 1.0 - span / 5.0); // Span > 5 frets is difficult

        // Penalize high fret positions
        var avgFret = playedPositions.Average(p => p.Fret.Value);
        var fretPenalty = Math.Max(0, 1.0 - avgFret / 12.0); // Higher frets are harder

        // Penalize stretches
        var maxStretch = 0;
        for (var i = 1; i < playedPositions.Count; i++)
        {
            var prev = playedPositions[i - 1];
            var curr = playedPositions[i];

            if (Math.Abs(curr.Str.Value - prev.Str.Value) == 1) // Adjacent strings
            {
                var stretch = Math.Abs(curr.Fret.Value - prev.Fret.Value);
                maxStretch = Math.Max(maxStretch, stretch);
            }
        }

        var stretchPenalty = Math.Max(0, 1.0 - maxStretch / 4.0); // Stretch > 4 frets is difficult

        // Combine penalties
        return (spanPenalty + fretPenalty + stretchPenalty) / 3.0;
    }

    /// <summary>
    ///     Generate a unique ID for this shape
    /// </summary>
    public static string GenerateId(string tuningId, IReadOnlyList<PositionLocation> positions)
    {
        var positionStr = string.Join("-", positions.Select(p => $"{p.Str.Value}:{p.Fret.Value}"));
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{tuningId}:{positionStr}")
        );
        return Convert.ToHexString(hash)[..16]; // First 16 chars
    }

    /// <summary>
    ///     Compute string mask (bit 0 = string 1, etc.)
    /// </summary>
    public static int ComputeStringMask(IReadOnlyList<PositionLocation> positions)
    {
        var mask = 0;
        foreach (var pos in positions.Where(p => !p.IsMuted))
        {
            mask |= 1 << pos.Str.Value - 1;
        }

        return mask;
    }

    public override string ToString()
    {
        var posStr = string.Join(" ", Positions.Select(p =>
            p.IsMuted ? "X" : p.Fret.Value.ToString()
        ));
        return $"{PitchClassSet} [{posStr}] (span:{Span}, diag:{Diagness:F2}, ergo:{Ergonomics:F2})";
    }
}
