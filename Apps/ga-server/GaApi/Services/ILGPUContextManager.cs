namespace GaApi.Services;

using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// Default implementation of ILGPU context manager
/// </summary>
public class IlgpuContextManager : IIlgpuContextManager
{
    private readonly ILogger<IlgpuContextManager> _logger;
    private bool _isDisposed;

    public Context? Context { get; private set; }

    public Accelerator? PrimaryAccelerator { get; }

    public string AcceleratorType { get; private set; } = "None";
    public bool IsGpuAvailable { get; private set; }
    public long AvailableGpuMemoryMb { get; private set; }
    public long TotalGpuMemoryMb { get; private set; }

    public IlgpuContextManager(ILogger<IlgpuContextManager> logger, Accelerator? primaryAccelerator)
    {
        _logger = logger;
        PrimaryAccelerator = primaryAccelerator;
        InitializeContext();
    }

    private void InitializeContext()
    {
        try
        {
            _logger.LogInformation("Initializing ILGPU context...");

            // Create default context
            Context = Context.CreateDefault();

            if (Context == null)
            {
                _logger.LogWarning("Failed to create ILGPU context");
                IsGpuAvailable = false;
                return;
            }

            // For now, use CPU-based computation
            // GPU acceleration can be added when ILGPU is properly configured
            AcceleratorType = "CPU";
            IsGpuAvailable = false;
            _logger.LogInformation("Using CPU-based computation (GPU acceleration not yet configured)");

            // Get memory information
            try
            {
                // Get accelerator memory size
                if (PrimaryAccelerator != null)
                {
                    TotalGpuMemoryMb = (long)(PrimaryAccelerator.MemorySize / (1024 * 1024));
                    AvailableGpuMemoryMb = TotalGpuMemoryMb;
                    _logger.LogInformation(
                        "Accelerator Memory: {TotalMB}MB total",
                        TotalGpuMemoryMb);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not query accelerator memory information");
            }

            _logger.LogInformation(
                "ILGPU initialized: {AcceleratorType}, GPU Available: {IsGpuAvailable}",
                AcceleratorType, IsGpuAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ILGPU context");
            IsGpuAvailable = false;
        }
    }

    /// <summary>
    /// Creates a new accelerator for specific device
    /// </summary>
    public Accelerator? CreateAccelerator(int deviceIndex = 0)
    {
        if (Context == null)
        {
            _logger.LogWarning("ILGPU context not initialized");
            return null;
        }

        try
        {
            // For now, return null to indicate CPU-based computation
            // GPU acceleration can be added when ILGPU is properly configured
            _logger.LogInformation("Using CPU-based computation (GPU acceleration not yet configured)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create accelerator for device {DeviceIndex}", deviceIndex);
            return null;
        }
    }

    /// <summary>
    /// Gets information about available accelerators
    /// </summary>
    public IEnumerable<string> GetAvailableAccelerators()
    {
        var accelerators = new List<string>();

        try
        {
            // Check for available accelerators
            // CUDA support can be added when ILGPU is properly configured
            accelerators.Add("CPU (Default)");
        }
        catch
        {
            // Fallback to CPU
        }

        // CPU is always available
        accelerators.Add("CPU Accelerator");

        return accelerators;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            PrimaryAccelerator?.Dispose();
            Context?.Dispose();
            _logger.LogInformation("ILGPU context disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ILGPU context");
        }

        _isDisposed = true;
    }
}

