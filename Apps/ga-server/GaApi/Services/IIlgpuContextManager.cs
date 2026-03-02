namespace GaApi.Services;

using ILGPU;
using ILGPU.Runtime;

/// <summary>
///     Manages ILGPU context and accelerator lifecycle
///     Provides singleton access to GPU resources across the application
///     Following ILGPU best practices: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
/// </summary>
public interface IIlgpuContextManager : IDisposable
{
    /// <summary>
    ///     Gets the ILGPU context
    /// </summary>
    Context? Context { get; }

    /// <summary>
    ///     Gets the primary accelerator (GPU or CPU fallback)
    /// </summary>
    Accelerator? PrimaryAccelerator { get; }

    /// <summary>
    ///     Gets the accelerator type (CUDA, CPU, etc.)
    /// </summary>
    string AcceleratorType { get; }

    /// <summary>
    ///     Indicates if GPU acceleration is available
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    ///     Gets available GPU memory in MB
    /// </summary>
    long AvailableGpuMemoryMb { get; }

    /// <summary>
    ///     Gets total GPU memory in MB
    /// </summary>
    long TotalGpuMemoryMb { get; }
}
