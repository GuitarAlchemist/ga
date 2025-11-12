namespace GA.Business.Core.Analysis.Gpu;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Atonal;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;

/// <summary>
///     Provides GPU-accelerated helpers for batch spectral analysis of set classes.
/// </summary>
/// <remarks>
///     This layer stays above GA.Business.Core so orchestration layers can opt into GPU acceleration
///     without polluting the domain model with ILGPU dependencies.
/// </remarks>
public sealed class SetClassGpuAnalyzer : IDisposable
{
    private const int PitchClassSpaceSize = 12;

    private readonly Context context;
    private readonly Accelerator accelerator;
    private readonly Action<Index1D, ArrayView<double>, ArrayView<double>, int> magnitudeKernel;

    /// <summary>
    ///     Initializes a new instance of the analyzer and creates an accelerator (CPU by default).
    /// </summary>
    public SetClassGpuAnalyzer(AcceleratorType preferredAccelerator = AcceleratorType.CPU)
    {
        context = Context.CreateDefault();
        accelerator = CreateAccelerator(context, preferredAccelerator);
        magnitudeKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>, int>(
            ComputeMagnitudeKernel);
    }

    /// <summary>
    ///     Gets the accelerator type used by this analyzer.
    /// </summary>
    public AcceleratorType AcceleratorType => accelerator.AcceleratorType;

    /// <summary>
    ///     Indicates whether the analyzer has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Computes magnitude spectra for a batch of set classes using ILGPU kernels.
    /// </summary>
    public IReadOnlyList<SetClassGpuSpectrum> AnalyzeSpectra(IReadOnlyList<SetClass> setClasses)
    {
        ArgumentNullException.ThrowIfNull(setClasses);
        if (setClasses.Count == 0)
        {
            return Array.Empty<SetClassGpuSpectrum>();
        }

        if (accelerator.AcceleratorType == AcceleratorType.CPU)
        {
            return setClasses
                .Select(sc => new SetClassGpuSpectrum(sc, sc.GetMagnitudeSpectrum(), sc.GetSpectralCentroid()))
                .ToList();
        }

        var pitchMatrix = BuildPitchClassMatrix(setClasses);
        var totalBins = pitchMatrix.Length;

        using var inputBuffer = accelerator.Allocate1D<double>(totalBins);
        inputBuffer.CopyFromCPU(pitchMatrix);

        using var magnitudeBuffer = accelerator.Allocate1D<double>(totalBins);

        magnitudeKernel(totalBins, inputBuffer.View, magnitudeBuffer.View, setClasses.Count);
        accelerator.Synchronize();

        var magnitudes = magnitudeBuffer.GetAsArray1D();
        return ProjectResults(setClasses, magnitudes);
    }

    private static double[] BuildPitchClassMatrix(IReadOnlyList<SetClass> setClasses)
    {
        var data = new double[setClasses.Count * PitchClassSpaceSize];

        for (var i = 0; i < setClasses.Count; i++)
        {
            var vector = setClasses[i].GetSpectralPrimeForm().ToBinaryVector(PitchClassSpaceSize);
            Array.Copy(vector, 0, data, i * PitchClassSpaceSize, PitchClassSpaceSize);
        }

        return data;
    }

    private static IReadOnlyList<SetClassGpuSpectrum> ProjectResults(IReadOnlyList<SetClass> setClasses, double[] magnitudes)
    {
        var results = new List<SetClassGpuSpectrum>(setClasses.Count);

        for (var i = 0; i < setClasses.Count; i++)
        {
            var slice = new double[PitchClassSpaceSize];
            Array.Copy(magnitudes, i * PitchClassSpaceSize, slice, 0, PitchClassSpaceSize);

            var centroid = ComputeSpectralCentroid(slice);
            results.Add(new SetClassGpuSpectrum(setClasses[i], slice, centroid));
        }

        return results;
    }

    private static double ComputeSpectralCentroid(IReadOnlyList<double> magnitudes)
    {
        double total = 0.0;
        double weighted = 0.0;

        for (var k = 0; k < magnitudes.Count; k++)
        {
            var value = magnitudes[k];
            total += value;
            weighted += k * value;
        }

        return total == 0 ? 0 : weighted / total;
    }

    private static void ComputeMagnitudeKernel(Index1D index, ArrayView<double> vectors, ArrayView<double> magnitude, int setCount)
    {
        var totalBins = setCount * PitchClassSpaceSize;
        if (index >= totalBins)
        {
            return;
        }

        var setIndex = index / PitchClassSpaceSize;
        var k = index % PitchClassSpaceSize;

        double real = 0.0;
        double imaginary = 0.0;

        var baseOffset = setIndex * PitchClassSpaceSize;

        for (var n = 0; n < PitchClassSpaceSize; n++)
        {
            var sample = vectors[baseOffset + n];
            var angle = -2.0 * XMath.PI * k * n / PitchClassSpaceSize;
            real += sample * XMath.Cos(angle);
            imaginary += sample * XMath.Sin(angle);
        }

        magnitude[index] = XMath.Sqrt(real * real + imaginary * imaginary);
    }

    private static Accelerator CreateAccelerator(Context ctx, AcceleratorType preferredAccelerator)
    {
        var preferredDevice = ctx.Devices.FirstOrDefault(device => device.AcceleratorType == preferredAccelerator);
        if (preferredDevice != null)
        {
            return preferredDevice.CreateAccelerator(ctx);
        }

        var fallbackDevice = ctx.Devices.First();
        return fallbackDevice.CreateAccelerator(ctx);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        accelerator.Dispose();
        context.Dispose();
        IsDisposed = true;
    }

    /// <summary>
    ///     Checks if a particular accelerator type is available in the current environment.
    /// </summary>
    public static bool IsAcceleratorAvailable(AcceleratorType acceleratorType)
    {
        using var ctx = Context.CreateDefault();
        return ctx.Devices.Any(device => device.AcceleratorType == acceleratorType);
    }
}
