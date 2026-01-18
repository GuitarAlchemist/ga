namespace GA.Business.Core.Invariants;

using System;
using System.Collections.Generic;
using System.Linq;

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

public enum InvariantSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
///     Composite result containing multiple validation results.
/// </summary>
public class CompositeInvariantValidationResult
{
    public bool IsValid => Results.All(r => r.IsValid);
    public List<InvariantValidationResult> Results { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<InvariantValidationResult> Failures => Results.Where(r => !r.IsValid);

    public IEnumerable<InvariantValidationResult> GetFailuresBySeverity(InvariantSeverity severity)
    {
        return Failures.Where(r => r.Severity == severity);
    }

    public void Add(InvariantValidationResult result)
    {
        Results.Add(result);
    }

    public void AddRange(IEnumerable<InvariantValidationResult> results)
    {
        Results.AddRange(results);
    }
}
