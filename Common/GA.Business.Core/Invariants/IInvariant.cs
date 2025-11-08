namespace GA.Business.Core.Invariants;

using System.ComponentModel.DataAnnotations;

/// <summary>
///     Base interface for all invariant validations
/// </summary>
public interface IInvariant<in T>
{
    /// <summary>
    ///     Name of the invariant for identification and logging
    /// </summary>
    string InvariantName { get; }

    /// <summary>
    ///     Description of what this invariant validates
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Severity level of invariant violations
    /// </summary>
    InvariantSeverity Severity { get; }

    /// <summary>
    ///     Category of the invariant for grouping and filtering
    /// </summary>
    string Category { get; }

    /// <summary>
    ///     Whether this invariant supports fast validation (for performance optimization)
    /// </summary>
    bool SupportsFastValidation { get; }

    /// <summary>
    ///     Estimated execution time for this invariant (for performance monitoring)
    /// </summary>
    TimeSpan EstimatedExecutionTime { get; }

    /// <summary>
    ///     Validates the invariant for the given object
    /// </summary>
    /// <param name="obj">Object to validate</param>
    /// <returns>Validation result with success status and error details</returns>
    InvariantValidationResult Validate(T obj);

    /// <summary>
    ///     Validates the invariant for the given object asynchronously
    /// </summary>
    /// <param name="obj">Object to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with success status and error details</returns>
    Task<InvariantValidationResult> ValidateAsync(T obj, CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of invariant validation
/// </summary>
public class InvariantValidationResult
{
    /// <summary>
    ///     Whether the invariant validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     Name of the invariant that was validated
    /// </summary>
    public string InvariantName { get; set; } = string.Empty;

    /// <summary>
    ///     Severity of the validation result
    /// </summary>
    public InvariantSeverity Severity { get; set; }

    /// <summary>
    ///     Category of the invariant
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    ///     Primary error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Multiple error messages if validation failed with multiple issues
    /// </summary>
    public List<string> ErrorMessages { get; set; } = [];

    /// <summary>
    ///     Name of the property that failed validation
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    ///     The value that was attempted to be set
    /// </summary>
    public object? AttemptedValue { get; set; }

    /// <summary>
    ///     Additional context information about the validation
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = [];

    /// <summary>
    ///     Timestamp when the validation was performed
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets all error messages (combines ErrorMessage and ErrorMessages)
    /// </summary>
    public IEnumerable<string> AllErrorMessages
    {
        get
        {
            var messages = new List<string>();
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                messages.Add(ErrorMessage);
            }

            messages.AddRange(ErrorMessages);
            return messages;
        }
    }
}

/// <summary>
///     Composite validation result for multiple invariants
/// </summary>
public class CompositeInvariantValidationResult
{
    /// <summary>
    ///     Individual validation results
    /// </summary>
    public List<InvariantValidationResult> Results { get; set; } = [];

    /// <summary>
    ///     Whether all invariants passed validation
    /// </summary>
    public bool IsValid => Results.All(r => r.IsValid);

    /// <summary>
    ///     Whether there are any critical failures
    /// </summary>
    public bool HasCriticalFailures => Results.Any(r => !r.IsValid && r.Severity == InvariantSeverity.Critical);

    /// <summary>
    ///     Whether there are any error-level failures
    /// </summary>
    public bool HasErrors => Results.Any(r => !r.IsValid && r.Severity >= InvariantSeverity.Error);

    /// <summary>
    ///     Whether there are any warnings
    /// </summary>
    public bool HasWarnings => Results.Any(r => !r.IsValid && r.Severity == InvariantSeverity.Warning);

    /// <summary>
    ///     Get all failed validation results
    /// </summary>
    public IEnumerable<InvariantValidationResult> Failures => Results.Where(r => !r.IsValid);

    /// <summary>
    ///     Get all successful validation results
    /// </summary>
    public IEnumerable<InvariantValidationResult> Successes => Results.Where(r => r.IsValid);

    /// <summary>
    ///     Get failures by severity level
    /// </summary>
    public IEnumerable<InvariantValidationResult> GetFailuresBySeverity(InvariantSeverity severity)
    {
        return Failures.Where(r => r.Severity == severity);
    }

    /// <summary>
    ///     Get failures by category
    /// </summary>
    public IEnumerable<InvariantValidationResult> GetFailuresByCategory(string category)
    {
        return Failures.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Get a summary of validation results
    /// </summary>
    public ValidationSummary GetSummary()
    {
        return new ValidationSummary
        {
            TotalInvariants = Results.Count,
            PassedInvariants = Successes.Count(),
            FailedInvariants = Failures.Count(),
            CriticalFailures = GetFailuresBySeverity(InvariantSeverity.Critical).Count(),
            Errors = GetFailuresBySeverity(InvariantSeverity.Error).Count(),
            Warnings = GetFailuresBySeverity(InvariantSeverity.Warning).Count(),
            InfoMessages = GetFailuresBySeverity(InvariantSeverity.Info).Count(),
            IsValid = IsValid,
            ValidatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
///     Summary of validation results
/// </summary>
public class ValidationSummary
{
    public int TotalInvariants { get; set; }
    public int PassedInvariants { get; set; }
    public int FailedInvariants { get; set; }
    public int CriticalFailures { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public int InfoMessages { get; set; }
    public bool IsValid { get; set; }
    public DateTime ValidatedAt { get; set; }

    public double SuccessRate => TotalInvariants > 0 ? (double)PassedInvariants / TotalInvariants : 0.0;
}

/// <summary>
///     Interface for objects that can be validated with invariants
/// </summary>
public interface IValidatable
{
    /// <summary>
    ///     Validates all invariants for this object
    /// </summary>
    CompositeInvariantValidationResult ValidateInvariants();
}

/// <summary>
///     Attribute to mark properties that should be validated by invariants
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class InvariantValidationAttribute(string invariantName) : ValidationAttribute
{
    public string InvariantName { get; } = invariantName;
    public InvariantSeverity Severity { get; set; } = InvariantSeverity.Error;

    public override bool IsValid(object? value)
    {
        // This will be implemented by specific invariant validators
        return true;
    }
}

/// <summary>
///     Exception thrown when critical invariants are violated
/// </summary>
public class InvariantViolationException : Exception
{
    public InvariantViolationException(CompositeInvariantValidationResult validationResult)
        : base($"Invariant violations detected: {validationResult.Failures.Count()} failures")
    {
        ValidationResult = validationResult;
    }

    public InvariantViolationException(CompositeInvariantValidationResult validationResult, string message)
        : base(message)
    {
        ValidationResult = validationResult;
    }

    public InvariantViolationException(CompositeInvariantValidationResult validationResult, string message,
        Exception innerException)
        : base(message, innerException)
    {
        ValidationResult = validationResult;
    }

    public CompositeInvariantValidationResult ValidationResult { get; }
}
