namespace GA.Domain.Core.Design;

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