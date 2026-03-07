namespace GA.Domain.Core.Design.Schema;

/// <summary>Named musical vocabulary constants used in domain attribute annotations.</summary>
/// <remarks>
/// Used by the type schema reflection system to surface domain rules and relationships
/// without coupling the domain model to infrastructure concerns.
/// </remarks>
public record DomainVocabulary(
    List<string> ChordQualities,
    List<string> Extensions,
    List<string> StackingTypes);
