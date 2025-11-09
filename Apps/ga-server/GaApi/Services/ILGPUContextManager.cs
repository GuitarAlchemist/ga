namespace GaApi.Services;

using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// Manages ILGPU context and accelerator lifecycle
/// Provides singleton access to GPU resources across the application
/// Following ILGPU best practices: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
/// </summary>
public interface IILGPUContextManager : IDisposable
{
    /// <summary>
    /// Gets the ILGPU context
    /// </summary>
    Context? Context { get; }

    /// <summary>
    /// Gets the primary accelerator (GPU or CPU fallback)
    /// </summary>
    Accelerator? PrimaryAccelerator { get; }

    /// <summary>
    /// Gets the accelerator type (CUDA, CPU, etc.)
    /// </summary>
    string AcceleratorType { get; }

    /// <summary>
    /// Indicates if GPU acceleration is available
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    /// Gets available GPU memory in MB
    /// </summary>
    long AvailableGpuMemoryMB { get; }

    /// <summary>
    /// Gets total GPU memory in MB
    /// </summary>
    long TotalGpuMemoryMB { get; }
}

/// <summary>
/// Default implementation of ILGPU context manager
/// </summary>
public class ILGPUContextManager : IILGPUContextManager
{
    private readonly ILogger<ILGPUContextManager> _logger;
    private Context? _context;
    private Accelerator? _primaryAccelerator;
    private bool _isDisposed;

    public Context? Context => _context;
    public Accelerator? PrimaryAccelerator => _primaryAccelerator;
    public string AcceleratorType { get; private set; } = "None";
    public bool IsGpuAvailable { get; private set; }
    public long AvailableGpuMemoryMB { get; private set; }
    public long TotalGpuMemoryMB { get; private set; }

    public ILGPUContextManager(ILogger<ILGPUContextManager> logger)
    {
        _logger = logger;
        InitializeContext();
    }

    private void InitializeContext()
    {
        try
        {
            _logger.LogInformation("Initializing ILGPU context...");

            // Create default context
            _context = Context.CreateDefault();

            if (_context == null)
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
                if (_primaryAccelerator != null)
                {
                    TotalGpuMemoryMB = (long)(_primaryAccelerator.MemorySize / (1024 * 1024));
                    AvailableGpuMemoryMB = TotalGpuMemoryMB;
                    _logger.LogInformation(
                        "Accelerator Memory: {TotalMB}MB total",
                        TotalGpuMemoryMB);
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
        if (_context == null)
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
            _primaryAccelerator?.Dispose();
            _context?.Dispose();
            _logger.LogInformation("ILGPU context disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ILGPU context");
        }

        _isDisposed = true;
    }
}

