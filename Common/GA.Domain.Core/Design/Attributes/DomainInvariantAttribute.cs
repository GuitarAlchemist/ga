namespace GA.Domain.Core.Design.Attributes;

/// <summary>
///     Specifies an invariant constraint for a domain entity.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = true)]
public class DomainInvariantAttribute(string description, string expression = "") : Attribute
{
    /// <summary>
    ///     Human-readable description of the invariant.
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    ///     Optional machine-readable expression or rule identifier.
    /// </summary>
    public string Expression { get; } = expression;
}
