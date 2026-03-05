namespace GA.Domain.Core.Design.Schema;

/// <summary>Metadata about a domain invariant (rule + predicate expression).</summary>
/// <remarks>
/// Used by the type schema reflection system to surface domain rules and relationships
/// without coupling the domain model to infrastructure concerns.
/// </remarks>
public record InvariantInfo(
    string Description,
    string Expression);
