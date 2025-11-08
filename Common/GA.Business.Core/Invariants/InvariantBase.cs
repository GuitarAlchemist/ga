namespace GA.Business.Core.Invariants;

/// <summary>
///     Abstract base class for invariant implementations
/// </summary>
public abstract class InvariantBase<T> : IInvariant<T>
{
    public abstract string InvariantName { get; }
    public abstract string Description { get; }
    public virtual InvariantSeverity Severity => InvariantSeverity.Error;
    public virtual string Category => "General";
    public virtual bool SupportsFastValidation => true;
    public virtual TimeSpan EstimatedExecutionTime => TimeSpan.FromMilliseconds(10);

    public abstract InvariantValidationResult Validate(T obj);

    /// <summary>
    ///     Default async implementation that wraps the synchronous Validate method
    /// </summary>
    public virtual Task<InvariantValidationResult> ValidateAsync(T obj, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = Validate(obj);
        return Task.FromResult(result);
    }

    /// <summary>
    ///     Helper method to create a successful validation result
    /// </summary>
    protected InvariantValidationResult Success()
    {
        return new InvariantValidationResult
        {
            IsValid = true,
            InvariantName = InvariantName,
            Severity = Severity,
            Category = Category
        };
    }

    /// <summary>
    ///     Helper method to create a failed validation result
    /// </summary>
    protected InvariantValidationResult Failure(string errorMessage, string? propertyName = null,
        object? attemptedValue = null)
    {
        return new InvariantValidationResult
        {
            IsValid = false,
            InvariantName = InvariantName,
            Severity = Severity,
            Category = Category,
            ErrorMessage = errorMessage,
            PropertyName = propertyName,
            AttemptedValue = attemptedValue
        };
    }

    /// <summary>
    ///     Helper method to create a failed validation result with multiple errors
    /// </summary>
    protected InvariantValidationResult Failure(IEnumerable<string> errorMessages)
    {
        return new InvariantValidationResult
        {
            IsValid = false,
            InvariantName = InvariantName,
            Severity = Severity,
            Category = Category,
            ErrorMessages = errorMessages.ToList()
        };
    }
}
