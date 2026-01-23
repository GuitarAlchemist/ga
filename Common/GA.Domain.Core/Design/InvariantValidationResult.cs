namespace GA.Domain.Core.Design;

using System;

/// <summary>
///     Result of an invariant validation.
/// </summary>
public class InvariantValidationResult
{
    public InvariantValidationResult() { }

    public InvariantValidationResult(bool isValid, string message, InvariantSeverity severity = InvariantSeverity.Error)
    {
        IsValid = isValid;
        Message = message;
        Severity = severity;
    }

    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public InvariantSeverity Severity { get; set; } = InvariantSeverity.Error;
    public string InvariantName { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    public static InvariantValidationResult Success() => new(true, "Validation successful", InvariantSeverity.Info);
    public static InvariantValidationResult Failure(string message, InvariantSeverity severity = InvariantSeverity.Error) => new(false, message, severity);
}