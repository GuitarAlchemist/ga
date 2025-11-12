namespace GA.Business.Core.Analysis.Gpu;

using System;
using GA.Business.Core.Atonal;

/// <summary>
///     Immutable spectral fingerprint produced by GPU kernels.
/// </summary>
public sealed record SetClassGpuSpectrum
{
    public SetClassGpuSpectrum(SetClass setClass, double[] magnitudeSpectrum, double spectralCentroid)
    {
        SetClass = setClass ?? throw new ArgumentNullException(nameof(setClass));
        MagnitudeSpectrum = magnitudeSpectrum ?? throw new ArgumentNullException(nameof(magnitudeSpectrum));
        SpectralCentroid = spectralCentroid;
    }

    public SetClass SetClass { get; }
    public double[] MagnitudeSpectrum { get; }
    public double SpectralCentroid { get; }
}
