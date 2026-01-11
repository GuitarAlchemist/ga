namespace GA.Business.Core.Atonal;

using System;

public static class PitchClassSetSpectralExtensions
{
    /// <summary>
    ///     Converts a pitch-class set into a binary incidence vector of fixed size (default 12).
    ///     Index n is 1.0 if pitch class n is in the set, otherwise 0.0.
    /// </summary>
    public static double[] ToBinaryVector(this PitchClassSet set, int size = 12)
    {
        ArgumentNullException.ThrowIfNull(set);

        var vector = new double[size];

        var mask = set.PitchClassMask;
        for (var i = 0; i < size; i++)
        {
            vector[i] = (mask & 1 << i % _pitchClassSpaceSize) != 0 ? 1.0 : 0.0;
        }

        return vector;
    }

    private const int _pitchClassSpaceSize = 12;
}
