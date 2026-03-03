namespace GaApi.Models;

/// <summary>
///     Standard error response for API controllers
/// </summary>
public class ErrorResponse
{
    /// <summary>
    ///     Short error code or type
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Optional detailed error information or stack trace
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    ///     Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
