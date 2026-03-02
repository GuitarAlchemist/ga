namespace GA.Domain.Services.Validation;

/// <summary>
///     Composite result containing multiple validation results.
/// </summary>
public class CompositeInvariantValidationResult
{
    public bool IsValid => Results.All(r => r.IsValid);
    public List<InvariantValidationResult> Results { get; init; } = [];
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    public IEnumerable<InvariantValidationResult> Failures => Results.Where(r => !r.IsValid);

    public IEnumerable<InvariantValidationResult> GetFailuresBySeverity(InvariantSeverity severity) => Failures.Where(r => r.Severity == severity);

    public void Add(InvariantValidationResult result) => Results.Add(result);

    public void AddRange(IEnumerable<InvariantValidationResult> results) => Results.AddRange(results);
}

