namespace GA.MusicTheory.Service.Models;

/// <summary>
///     Validation error details
/// </summary>
public class ValidationError
{
    /// <summary>
    ///     Field name that failed validation
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    ///     Validation error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Attempted value
    /// </summary>
    public object? AttemptedValue { get; set; }

    public ValidationError() { }

    public ValidationError(string field, string message, object? attemptedValue = null)
    {
        Field = field;
        Message = message;
        AttemptedValue = attemptedValue;
    }
}
