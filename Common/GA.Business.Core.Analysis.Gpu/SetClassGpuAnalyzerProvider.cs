namespace GA.Business.Core.Analysis.Gpu;

using System;
using System.Collections.Concurrent;
using ILGPU.Runtime;

/// <summary>
///     Simple cache/factory that hands out shared analyzer instances per accelerator type.
/// </summary>
public static class SetClassGpuAnalyzerProvider
{
    private static readonly ConcurrentDictionary<AcceleratorType, Lazy<SetClassGpuAnalyzer>> AnalyzerCache = new();

    /// <summary>
    ///     Gets an analyzer, preferring CUDA if available and falling back to CPU otherwise.
    /// </summary>
    public static SetClassGpuAnalyzer GetAnalyzer(bool preferGpu = true)
    {
        if (preferGpu && SetClassGpuAnalyzer.IsAcceleratorAvailable(AcceleratorType.Cuda))
        {
            return GetOrCreate(AcceleratorType.Cuda);
        }

        return GetOrCreate(AcceleratorType.CPU);
    }

    /// <summary>
    ///     Gets an analyzer for a specific accelerator type. Will throw if the accelerator does not exist.
    /// </summary>
    public static SetClassGpuAnalyzer GetAnalyzer(AcceleratorType acceleratorType)
    {
        if (!SetClassGpuAnalyzer.IsAcceleratorAvailable(acceleratorType))
        {
            throw new InvalidOperationException($"Accelerator '{acceleratorType}' is not available on this machine.");
        }

        return GetOrCreate(acceleratorType);
    }

    private static SetClassGpuAnalyzer GetOrCreate(AcceleratorType acceleratorType)
    {
        var lazy = AnalyzerCache.GetOrAdd(acceleratorType, type => new Lazy<SetClassGpuAnalyzer>(() => new SetClassGpuAnalyzer(type)));
        var analyzer = lazy.Value;

        if (analyzer.IsDisposed)
        {
            AnalyzerCache[acceleratorType] = new Lazy<SetClassGpuAnalyzer>(() => new SetClassGpuAnalyzer(acceleratorType));
            analyzer = AnalyzerCache[acceleratorType].Value;
        }

        return analyzer;
    }
}
