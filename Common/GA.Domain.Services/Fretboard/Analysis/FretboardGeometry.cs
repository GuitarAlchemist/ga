namespace GA.Domain.Services.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using Core.Instruments.Biomechanics;

/// <summary>
/// Centralized service for fretboard physical geometry calculations.
/// Implements standard logarithmic fret spacing.
/// </summary>
public static class FretboardGeometry
{
    /// <summary>
    /// Calculates the physical distance between two frets relative to scale length.
    /// Formula: d = 2^(-low/12) - 2^(-high/12)
    /// </summary>
    public static double CalculatePhysicalDistance(int fretLow, int fretHigh)
    {
        if (fretLow == fretHigh) return 0;
        if (fretLow > fretHigh) (fretLow, fretHigh) = (fretHigh, fretLow);
        
        double low = Math.Max(0, fretLow);
        double high = Math.Max(0, fretHigh);

        return Math.Pow(2, -low / (double)BiomechanicsConstants.SemitonesPerOctave) - 
               Math.Pow(2, -high / (double)BiomechanicsConstants.SemitonesPerOctave);
    }

    /// <summary>
    /// Calculates the absolute distance in millimeters for a given scale length.
    /// </summary>
    public static double CalculateSpanMm(int fretLow, int fretHigh, double scaleLength = BiomechanicsConstants.StandardScaleLengthMm) => CalculatePhysicalDistance(fretLow, fretHigh) * scaleLength;

    /// <summary>
    /// Calculates the physical span of a set of frets.
    /// Considers only fingered notes (fret > 0).
    /// </summary>
    public static double CalculatePhysicalSpan(ReadOnlySpan<int> frets)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        var count = 0;

        foreach (var f in frets)
        {
            if (f > 0)
            {
                if (f < min) min = f;
                if (f > max) max = f;
                count++;
            }
        }

        if (count < 2) return 0;

        return CalculatePhysicalDistance(min, max);
    }

    /// <summary>
    /// Overload for IEnumerable compatibility
    /// </summary>
    public static double CalculatePhysicalSpan(IEnumerable<int> frets) => 
        CalculatePhysicalSpan(frets is int[] arr ? arr.AsSpan() : [.. frets]);

    /// <summary>
    /// Returns a normalized effort score for a physical span.
    /// </summary>
    public static double GetSpanEffortScore(double physicalSpan)
    {
        var limit = BiomechanicsConstants.ComfortableReachScaleUnits;
        if (physicalSpan <= limit) return physicalSpan / limit; 
        return 1.0 + (physicalSpan - limit) * 5.0; 
    }
}
