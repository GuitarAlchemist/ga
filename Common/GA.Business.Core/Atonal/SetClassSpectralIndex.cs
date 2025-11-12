namespace GA.Business.Core.Atonal;

using System;
using System.Collections.Generic;
using System.Linq;

public static class SetClassSpectralIndex
{
    private static readonly Lazy<List<(SetClass SetClass, double[] Spectrum)>> _index =
        new(() => BuildIndex());

    private static List<(SetClass SetClass, double[] Spectrum)> BuildIndex()
    {
        return SetClass.Items
            .Select(setClass => (SetClass: setClass, Spectrum: setClass.GetMagnitudeSpectrum()))
            .ToList();
    }

    /// <summary>
    ///     Returns the N nearest neighbours to the source set class
    ///     according to L1 spectral distance on the magnitude spectrum.
    /// </summary>
    public static IReadOnlyList<SetClass> GetNearestBySpectrum(SetClass source, int count = 8)
    {
        ArgumentNullException.ThrowIfNull(source);

        var targetCount = Math.Max(0, count);
        var sourceSpectrum = source.GetMagnitudeSpectrum();

        return _index.Value
            .Where(entry => !ReferenceEquals(entry.SetClass, source))
            .OrderBy(entry => SpectralDistance(sourceSpectrum, entry.Spectrum))
            .Take(targetCount)
            .Select(entry => entry.SetClass)
            .ToList();
    }

    /// <summary>
    ///     Simple L1 distance between two spectra.
    /// </summary>
    private static double SpectralDistance(double[] a, double[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        var sum = 0.0;

        for (var i = 0; i < len; i++)
        {
            sum += Math.Abs(a[i] - b[i]);
        }

        return sum;
    }
}
