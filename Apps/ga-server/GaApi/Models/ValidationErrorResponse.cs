namespace GaApi.Models;

/// <summary>
///     Validation error response
/// </summary>
public class ValidationErrorResponse
{
    /// <summary>
    ///     List of validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = [];

    /// <summary>
    ///     Overall validation message
    /// </summary>
    public string Message { get; set; } = "Validation failed";

    /// <summary>
    ///     Timestamp of the validation error
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
